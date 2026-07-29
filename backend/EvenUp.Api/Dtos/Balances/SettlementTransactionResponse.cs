namespace EvenUp.Api.Dtos.Balances;

public class SettlementTransactionResponse
{
    public Guid FromMemberId { get; set; }
    public string FromDisplayName { get; set; } = string.Empty;
    public Guid ToMemberId { get; set; }
    public string ToDisplayName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
