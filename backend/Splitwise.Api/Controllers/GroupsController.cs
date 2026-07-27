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
}
