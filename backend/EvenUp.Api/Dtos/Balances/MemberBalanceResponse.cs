namespace EvenUp.Api.Dtos.Balances;

public class MemberBalanceResponse
{
    public Guid MemberId { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    // Positive = this member is owed money (creditor). Negative = this member owes money (debtor).
    public decimal NetBalance { get; set; }
}
