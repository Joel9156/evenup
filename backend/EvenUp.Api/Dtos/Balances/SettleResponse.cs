namespace EvenUp.Api.Dtos.Balances;

public class SettleResponse
{
    public Guid SettlementId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<SettlementTransactionResponse> Transactions { get; set; } = [];
}
