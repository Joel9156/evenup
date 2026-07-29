namespace EvenUp.Api.Dtos.Balances;

public class BalancesResponse
{
    public List<MemberBalanceResponse> NetBalances { get; set; } = [];

    // The minimum-transfer-count plan to bring everyone's balance to zero, computed live —
    // nothing is persisted until POST /settle is called.
    public List<SettlementTransactionResponse> SuggestedTransactions { get; set; } = [];
}
