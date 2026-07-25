namespace Splitwise.Api.Models;

public class ExpenseShare
{
    public Guid Id { get; set; }

    public Guid ExpenseId { get; set; }
    public Expense Expense { get; set; } = null!;

    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public decimal ShareAmount { get; set; }
}
