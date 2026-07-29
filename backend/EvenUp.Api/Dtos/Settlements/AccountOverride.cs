using System.ComponentModel.DataAnnotations;

namespace EvenUp.Api.Dtos.Settlements;

// Supplies bank details for a member who has none on file — a guest, or a sign-in user who
// never registered an account. Entered on the spot in the settle screen, never saved to DB.
public class AccountOverride
{
    [Required]
    public Guid MemberId { get; set; }

    [Required, MaxLength(100)]
    public string BankName { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string AccountNumber { get; set; } = string.Empty;
}
