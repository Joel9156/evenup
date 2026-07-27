using Splitwise.Api.Dtos.Balances;

namespace Splitwise.Api.Services;

public interface IBalanceService
{
    Task<BalancesResponse?> GetBalancesAsync(Guid groupId, CancellationToken ct = default);
    Task<SettleResponse?> SettleAsync(Guid groupId, CancellationToken ct = default);
}
