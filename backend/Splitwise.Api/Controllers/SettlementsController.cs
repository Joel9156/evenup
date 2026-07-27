using Microsoft.AspNetCore.Mvc;
using Splitwise.Api.Dtos.Settlements;
using Splitwise.Api.Services;

namespace Splitwise.Api.Controllers;

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
