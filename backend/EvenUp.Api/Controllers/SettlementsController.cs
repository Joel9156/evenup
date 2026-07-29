using Microsoft.AspNetCore.Mvc;
using EvenUp.Api.Dtos.Settlements;
using EvenUp.Api.Services;

namespace EvenUp.Api.Controllers;

[ApiController]
[Route("api/settlements/{settlementId:guid}")]
public class SettlementsController(ISettlementMessageService settlementMessageService) : ControllerBase
{
    [HttpPost("messages")]
    public async Task<ActionResult<List<SettlementMessageResponse>>> GenerateMessages(
        Guid settlementId, GenerateSettlementMessagesRequest request, CancellationToken ct)
    {
        var messages = await settlementMessageService.GenerateMessagesAsync(settlementId, request, ct);
        return messages is null ? NotFound() : Ok(messages);
    }
}
