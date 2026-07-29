namespace EvenUp.Api.Dtos.Expenses;

public class ExpenseShareResponse
{
    public Guid MemberId { get; set; }
    public string MemberDisplayName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
