using EvenUp.Api.Dtos.Settlements;

namespace EvenUp.Api.Services;

public interface ISettlementMessageService
{
    Task<List<SettlementMessageResponse>?> GenerateMessagesAsync(Guid settlementId, GenerateSettlementMessagesRequest request, CancellationToken ct = default);
}
