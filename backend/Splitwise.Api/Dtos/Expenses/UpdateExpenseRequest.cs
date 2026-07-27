using System.ComponentModel.DataAnnotations;

namespace Splitwise.Api.Dtos.Expenses;

public class UpdateExpenseRequest
{
    [Required, MinLength(1), MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal TotalAmount { get; set; }

    [Required]
    public Guid PaidByMemberId { get; set; }

    [Required, MinLength(1)]
    public List<ExpenseShareRequest> Shares { get; set; } = [];
}
