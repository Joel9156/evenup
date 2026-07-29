using System.ComponentModel.DataAnnotations;

namespace EvenUp.Api.Dtos.Auth;

public class UpdateAccountRequest
{
    [Required, MaxLength(100)]
    public string BankName { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string AccountNumber { get; set; } = string.Empty;
}
