using Splitwise.Api.Dtos.Groups;

namespace Splitwise.Api.Services;

public interface IGroupService
{
    Task<GroupResponse> CreateGroupAsync(Guid creatorUserId, CreateGroupRequest request, CancellationToken ct = default);
    Task<GroupResponse?> GetGroupAsync(Guid groupId, CancellationToken ct = default);
    Task<GroupPreviewResponse?> GetGroupPreviewAsync(string inviteCode, CancellationToken ct = default);
    Task<JoinGroupResponse?> JoinGroupAsync(Guid groupId, Guid? signedInUserId, JoinGroupRequest request, CancellationToken ct = default);
}
