using Splitwise.Api.Dtos.Settlements;

namespace Splitwise.Api.Services;

public interface ISettlementMessageService
{
    Task<List<SettlementMessageResponse>?> GenerateMessagesAsync(Guid settlementId, GenerateSettlementMessagesRequest request, CancellationToken ct = default);
}
