using Microsoft.AspNetCore.Mvc;
using Splitwise.Api.Dtos.Balances;
using Splitwise.Api.Services;

namespace Splitwise.Api.Controllers;

[ApiController]
[Route("api/groups/{groupId:guid}")]
public class BalancesController(IBalanceService balanceService) : ControllerBase
{
    [HttpGet("balances")]
    public async Task<ActionResult<BalancesResponse>> GetBalances(Guid groupId, CancellationToken ct)
    {
        var balances = await balanceService.GetBalancesAsync(groupId, ct);
        return balances is null ? NotFound() : Ok(balances);
    }

    [HttpPost("settle")]
    public async Task<ActionResult<SettleResponse>> Settle(Guid groupId, CancellationToken ct)
    {
        var result = await balanceService.SettleAsync(groupId, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
