using System.ComponentModel.DataAnnotations;

namespace EvenUp.Api.Dtos.Auth;

public class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required, MinLength(1), MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;
}
