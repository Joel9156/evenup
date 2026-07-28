using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Splitwise.Api.Dtos.Groups;
using Splitwise.Api.Extensions;
using Splitwise.Api.Services;

namespace Splitwise.Api.Controllers;

[ApiController]
[Route("api/groups")]
public class GroupsController(IGroupService groupService) : ControllerBase
{
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<List<GroupResponse>>> GetMine(CancellationToken ct)
    {
        var groups = await groupService.GetMyGroupsAsync(User.GetUserId(), ct);
        return Ok(groups);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<GroupResponse>> Create(CreateGroupRequest request, CancellationToken ct)
    {
        var group = await groupService.CreateGroupAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(GetById), new { id = group.Id }, group);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GroupResponse>> GetById(Guid id, CancellationToken ct)
    {
        var group = await groupService.GetGroupAsync(id, ct);
        return group is null ? NotFound() : Ok(group);
    }

    [HttpGet("join/{inviteCode}")]
    public async Task<ActionResult<GroupPreviewResponse>> Preview(string inviteCode, CancellationToken ct)
    {
        var preview = await groupService.GetGroupPreviewAsync(inviteCode, ct);
        return preview is null ? NotFound() : Ok(preview);
    }

    // Public: works both for a signed-in user (attaches a real Authorization header) and
    // an anonymous guest (no header at all) — whichever the caller sends, if any.
    [HttpPost("{id:guid}/join")]
    public async Task<ActionResult<JoinGroupResponse>> Join(Guid id, JoinGroupRequest request, CancellationToken ct)
    {
        Guid? signedInUserId = User.Identity?.IsAuthenticated == true ? User.GetUserId() : null;

        var result = await groupService.JoinGroupAsync(id, signedInUserId, request, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [Authorize]
    [HttpPost("{id:guid}/members")]
    public async Task<ActionResult<MemberResponse>> AddMember(Guid id, AddMemberRequest request, CancellationToken ct)
    {
        var result = await groupService.AddMemberAsync(id, User.GetUserId(), request, ct);
        return result.Error switch
        {
            AddMemberError.None => Ok(result.Member),
            AddMemberError.GroupNotFound => NotFound(new { message = "Group not found." }),
            AddMemberError.Forbidden => Forbid(),
            _ => BadRequest(),
        };
    }

    [Authorize]
    [HttpPut("{id:guid}/members/{memberId:guid}")]
    public async Task<ActionResult<MemberResponse>> UpdateMember(Guid id, Guid memberId, UpdateMemberRequest request, CancellationToken ct)
    {
        var result = await groupService.UpdateMemberAsync(id, memberId, User.GetUserId(), request, ct);
        return result.Error switch
        {
            UpdateMemberError.None => Ok(result.Member),
            UpdateMemberError.GroupNotFound => NotFound(new { message = "Group not found." }),
            UpdateMemberError.MemberNotFound => NotFound(new { message = "Member not found." }),
            UpdateMemberError.Forbidden => Forbid(),
            _ => BadRequest(),
        };
    }

    [Authorize]
    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberId, CancellationToken ct)
    {
        var result = await groupService.RemoveMemberAsync(id, memberId, User.GetUserId(), ct);
        return result.Error switch
        {
            RemoveMemberError.None => NoContent(),
            RemoveMemberError.GroupNotFound => NotFound(new { message = "Group not found." }),
            RemoveMemberError.MemberNotFound => NotFound(new { message = "Member not found." }),
            RemoveMemberError.Forbidden => Forbid(),
            RemoveMemberError.MemberHasExpenses => BadRequest(new { message = "This member is involved in existing expenses and can't be removed." }),
            RemoveMemberError.LastSignedInMember => BadRequest(new { message = "A group needs at least one signed-in member." }),
            _ => BadRequest(),
        };
    }
}
