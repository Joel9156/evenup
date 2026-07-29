namespace EvenUp.Api.Dtos.Auth;

public class MeResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? BankName { get; set; }
    public bool HasAccountNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}
