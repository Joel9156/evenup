namespace Splitwise.Api.Models;

public class Expense
{
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;

    public Guid PaidByMemberId { get; set; }
    public Member PaidByMember { get; set; } = null!;

    // Used to check edit/delete permission: only the creator (sign-in user) may edit or delete.
    public Guid CreatedByMemberId { get; set; }
    public Member CreatedByMember { get; set; } = null!;

    public string Description { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<ExpenseShare> Shares { get; set; } = new List<ExpenseShare>();
}
