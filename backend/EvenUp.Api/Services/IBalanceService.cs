using EvenUp.Api.Dtos.Balances;

namespace EvenUp.Api.Services;

public interface IBalanceService
{
    Task<BalancesResponse?> GetBalancesAsync(Guid groupId, CancellationToken ct = default);
    Task<SettleResponse?> SettleAsync(Guid groupId, CancellationToken ct = default);
}
