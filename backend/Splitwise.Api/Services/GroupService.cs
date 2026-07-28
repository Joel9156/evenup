using Microsoft.EntityFrameworkCore;
using Splitwise.Api.Data;
using Splitwise.Api.Dtos.Groups;
using Splitwise.Api.Models;

namespace Splitwise.Api.Services;

public class GroupService(SplitwiseDbContext db, IInviteCodeGenerator inviteCodeGenerator) : IGroupService
{
    public async Task<List<GroupResponse>> GetMyGroupsAsync(Guid userId, CancellationToken ct = default)
    {
        var groups = await db.Groups
            .Include(g => g.Members)
            .Where(g => g.Members.Any(m => m.UserId == userId))
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(ct);

        return groups.Select(g => ToGroupResponse(g, g.Members)).ToList();
    }

    public async Task<GroupResponse> CreateGroupAsync(Guid creatorUserId, CreateGroupRequest request, CancellationToken ct = default)
    {
        var creator = await db.Users.FindAsync([creatorUserId], ct)
            ?? throw new InvalidOperationException("Authenticated user was not found.");

        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            InviteCode = await GenerateUniqueInviteCodeAsync(ct),
            CreatedByUserId = creatorUserId,
            CreatedAt = DateTime.UtcNow,
        };

        // The creator is always a sign-in user and always the group's first member.
        var creatorMember = new Member
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            UserId = creatorUserId,
            DisplayName = creator.DisplayName,
            IsGuest = false,
            JoinedAt = DateTime.UtcNow,
        };

        db.Groups.Add(group);
        db.Members.Add(creatorMember);
        await db.SaveChangesAsync(ct);

        return ToGroupResponse(group, [creatorMember]);
    }

    public async Task<GroupResponse?> GetGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        var group = await db.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId, ct);

        return group is null ? null : ToGroupResponse(group, group.Members);
    }

    public async Task<GroupPreviewResponse?> GetGroupPreviewAsync(string inviteCode, CancellationToken ct = default)
    {
        var group = await db.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.InviteCode == inviteCode, ct);

        if (group is null)
        {
            return null;
        }

        return new GroupPreviewResponse
        {
            GroupId = group.Id,
            GroupName = group.Name,
            MemberNames = group.Members.Select(m => m.DisplayName).ToList(),
            ClaimableMembers = group.Members
                .Where(m => m.IsGuest)
                .Select(m => new PreviewMemberResponse { Id = m.Id, DisplayName = m.DisplayName })
                .ToList(),
        };
    }

    public async Task<JoinGroupResponse?> JoinGroupAsync(Guid groupId, Guid? signedInUserId, JoinGroupRequest request, CancellationToken ct = default)
    {
        var group = await db.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId, ct);

        if (group is null)
        {
            return null;
        }

        if (signedInUserId is Guid userId)
        {
            // Joining twice shouldn't create a duplicate membership — return the existing one.
            var existingMember = group.Members.FirstOrDefault(m => m.UserId == userId);
            if (existingMember is not null)
            {
                return ToJoinResponse(existingMember);
            }

            // Claim an existing unclaimed guest placeholder instead of creating a duplicate
            // person, if the caller pointed at one (from the invite preview's
            // ClaimableMembers) and it's still unclaimed. An invalid/already-claimed id is
            // ignored rather than treated as an error — falls through to creating a normal
            // new member below, same as if no id had been supplied at all.
            if (request.ExistingMemberId is Guid existingMemberId)
            {
                var claimable = group.Members.FirstOrDefault(m => m.Id == existingMemberId && m.UserId == null);
                if (claimable is not null)
                {
                    claimable.UserId = userId;
                    claimable.IsGuest = false;
                    await db.SaveChangesAsync(ct);
                    return ToJoinResponse(claimable);
                }
            }

            var member = new Member
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                UserId = userId,
                DisplayName = request.DisplayName.Trim(),
                IsGuest = false,
                JoinedAt = DateTime.UtcNow,
            };

            db.Members.Add(member);
            await db.SaveChangesAsync(ct);
            return ToJoinResponse(member);
        }
        else
        {
            var guestMember = new Member
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                UserId = null,
                DisplayName = request.DisplayName.Trim(),
                IsGuest = true,
                JoinedAt = DateTime.UtcNow,
            };

            db.Members.Add(guestMember);
            await db.SaveChangesAsync(ct);
            return ToJoinResponse(guestMember);
        }
    }

    public async Task<AddMemberResult> AddMemberAsync(Guid groupId, Guid requestingUserId, AddMemberRequest request, CancellationToken ct = default)
    {
        var group = await db.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId, ct);

        if (group is null)
        {
            return AddMemberResult.Fail(AddMemberError.GroupNotFound);
        }

        if (!group.Members.Any(m => m.UserId == requestingUserId))
        {
            return AddMemberResult.Fail(AddMemberError.Forbidden);
        }

        var member = new Member
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            UserId = null,
            DisplayName = request.DisplayName.Trim(),
            IsGuest = true,
            JoinedAt = DateTime.UtcNow,
        };

        db.Members.Add(member);
        await db.SaveChangesAsync(ct);

        return AddMemberResult.Ok(new MemberResponse
        {
            Id = member.Id,
            UserId = member.UserId,
            DisplayName = member.DisplayName,
            IsGuest = member.IsGuest,
            JoinedAt = member.JoinedAt,
        });
    }

    public async Task<UpdateMemberResult> UpdateMemberAsync(Guid groupId, Guid memberId, Guid requestingUserId, UpdateMemberRequest request, CancellationToken ct = default)
    {
        var group = await db.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId, ct);

        if (group is null)
        {
            return UpdateMemberResult.Fail(UpdateMemberError.GroupNotFound);
        }

        if (!group.Members.Any(m => m.UserId == requestingUserId))
        {
            return UpdateMemberResult.Fail(UpdateMemberError.Forbidden);
        }

        var member = group.Members.FirstOrDefault(m => m.Id == memberId);
        if (member is null)
        {
            return UpdateMemberResult.Fail(UpdateMemberError.MemberNotFound);
        }

        member.DisplayName = request.DisplayName.Trim();
        await db.SaveChangesAsync(ct);

        return UpdateMemberResult.Ok(new MemberResponse
        {
            Id = member.Id,
            UserId = member.UserId,
            DisplayName = member.DisplayName,
            IsGuest = member.IsGuest,
            JoinedAt = member.JoinedAt,
        });
    }

    public async Task<RemoveMemberResult> RemoveMemberAsync(Guid groupId, Guid memberId, Guid requestingUserId, CancellationToken ct = default)
    {
        var group = await db.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId, ct);

        if (group is null)
        {
            return RemoveMemberResult.Fail(RemoveMemberError.GroupNotFound);
        }

        if (!group.Members.Any(m => m.UserId == requestingUserId))
        {
            return RemoveMemberResult.Fail(RemoveMemberError.Forbidden);
        }

        var member = group.Members.FirstOrDefault(m => m.Id == memberId);
        if (member is null)
        {
            return RemoveMemberResult.Fail(RemoveMemberError.MemberNotFound);
        }

        if (member.UserId is not null && group.Members.Count(m => m.UserId != null) == 1)
        {
            return RemoveMemberResult.Fail(RemoveMemberError.LastSignedInMember);
        }

        var hasExpenseHistory = await db.Expenses.AnyAsync(e => e.PaidByMemberId == memberId || e.CreatedByMemberId == memberId, ct)
            || await db.ExpenseShares.AnyAsync(s => s.MemberId == memberId, ct);

        if (hasExpenseHistory)
        {
            return RemoveMemberResult.Fail(RemoveMemberError.MemberHasExpenses);
        }

        db.Members.Remove(member);
        await db.SaveChangesAsync(ct);

        return RemoveMemberResult.Ok();
    }

    private async Task<string> GenerateUniqueInviteCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = inviteCodeGenerator.Generate();
            var exists = await db.Groups.AnyAsync(g => g.InviteCode == code, ct);
            if (!exists)
            {
                return code;
            }
        }

        throw new InvalidOperationException("Failed to generate a unique invite code after several attempts.");
    }

    private static GroupResponse ToGroupResponse(Group group, IEnumerable<Member> members) => new()
    {
        Id = group.Id,
        Name = group.Name,
        InviteCode = group.InviteCode,
        CreatedAt = group.CreatedAt,
        Members = members.Select(m => new MemberResponse
        {
            Id = m.Id,
            UserId = m.UserId,
            DisplayName = m.DisplayName,
            IsGuest = m.IsGuest,
            JoinedAt = m.JoinedAt,
        }).ToList(),
    };

    private static JoinGroupResponse ToJoinResponse(Member member) => new()
    {
        MemberId = member.Id,
        GroupId = member.GroupId,
        DisplayName = member.DisplayName,
        IsGuest = member.IsGuest,
    };
}
