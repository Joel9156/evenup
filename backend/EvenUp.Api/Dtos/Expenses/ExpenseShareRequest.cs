using System.ComponentModel.DataAnnotations;

namespace EvenUp.Api.Dtos.Expenses;

public class ExpenseShareRequest
{
    [Required]
    public Guid MemberId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }
}
