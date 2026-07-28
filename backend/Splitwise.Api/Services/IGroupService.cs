using Splitwise.Api.Dtos.Groups;

namespace Splitwise.Api.Services;

public interface IGroupService
{
    Task<List<GroupResponse>> GetMyGroupsAsync(Guid userId, CancellationToken ct = default);
    Task<GroupResponse> CreateGroupAsync(Guid creatorUserId, CreateGroupRequest request, CancellationToken ct = default);
    Task<GroupResponse?> GetGroupAsync(Guid groupId, CancellationToken ct = default);
    Task<GroupPreviewResponse?> GetGroupPreviewAsync(string inviteCode, CancellationToken ct = default);
    Task<JoinGroupResponse?> JoinGroupAsync(Guid groupId, Guid? signedInUserId, JoinGroupRequest request, CancellationToken ct = default);

    // Lets an existing sign-in member add a placeholder member directly — no invite link
    // needed. Useful when one person tracks everything themselves and only ever sends the
    // others a settlement message, without expecting them to open the app at all. The
    // created member is structurally identical to a guest who joined via invite (UserId
    // null, IsGuest true) — this is just an alternate way to create that same row.
    Task<AddMemberResult> AddMemberAsync(Guid groupId, Guid requestingUserId, AddMemberRequest request, CancellationToken ct = default);

    // Renaming and removing are both restricted to signed-in members of the group (same gate
    // as AddMemberAsync) — there's no separate "owner" role in this app's permission model.
    Task<UpdateMemberResult> UpdateMemberAsync(Guid groupId, Guid memberId, Guid requestingUserId, UpdateMemberRequest request, CancellationToken ct = default);
    Task<RemoveMemberResult> RemoveMemberAsync(Guid groupId, Guid memberId, Guid requestingUserId, CancellationToken ct = default);
}
