using Microsoft.EntityFrameworkCore;
using Splitwise.Api.Data;
using Splitwise.Api.Dtos.Groups;
using Splitwise.Api.Models;

namespace Splitwise.Api.Services;

public class GroupService(SplitwiseDbContext db, IInviteCodeGenerator inviteCodeGenerator) : IGroupService
{
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
            GroupName = group.Name,
            MemberNames = group.Members.Select(m => m.DisplayName).ToList(),
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
