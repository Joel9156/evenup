using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EvenUp.Api.Dtos.AiChat;
using EvenUp.Api.Extensions;
using EvenUp.Api.Services;

namespace EvenUp.Api.Controllers;

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
