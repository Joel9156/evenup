using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Splitwise.Api.Dtos.AiChat;
using Splitwise.Api.Extensions;
using Splitwise.Api.Services;

namespace Splitwise.Api.Controllers;

[ApiController]
[Route("api/groups/{groupId:guid}/ai-chat")]
public class AiChatController(IAiChatService aiChatService) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<AiChatResponse>> Chat(Guid groupId, AiChatRequest request, CancellationToken ct)
    {
        var result = await aiChatService.ProcessMessageAsync(groupId, User.GetUserId(), request, ct);
        return result.Error switch
        {
            AiChatError.None => Ok(result.Value),
            AiChatError.GroupNotFound => NotFound(new { message = "Group not found." }),
            AiChatError.Forbidden => Forbid(),
            _ => BadRequest(),
        };
    }
}
