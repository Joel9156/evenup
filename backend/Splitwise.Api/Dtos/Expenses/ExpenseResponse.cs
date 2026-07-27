namespace Splitwise.Api.Dtos.Expenses;

public class ExpenseResponse
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public Guid PaidByMemberId { get; set; }
    public string PaidByDisplayName { get; set; } = string.Empty;
    public Guid CreatedByMemberId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ExpenseShareResponse> Shares { get; set; } = [];
}
