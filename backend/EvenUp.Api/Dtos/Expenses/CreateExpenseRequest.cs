using System.ComponentModel.DataAnnotations;

namespace EvenUp.Api.Dtos.Expenses;

public class CreateExpenseRequest
{
    [Required, MinLength(1), MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal TotalAmount { get; set; }

    [Required]
    public Guid PaidByMemberId { get; set; }

    // Which member is submitting this entry — a guest has no JWT to prove identity, so the
    // client tells us who it's acting as. This is also what future edit/delete checks key off.
    [Required]
    public Guid CreatedByMemberId { get; set; }

    [Required, MinLength(1)]
    public List<ExpenseShareRequest> Shares { get; set; } = [];
}
