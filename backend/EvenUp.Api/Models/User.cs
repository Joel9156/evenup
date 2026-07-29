namespace EvenUp.Api.Models;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    // AES-256 encrypted (reversible) — different from PasswordHash, which is a one-way hash.
    public string? AccountNumberEncrypted { get; set; }
    public string? BankName { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<Group> CreatedGroups { get; set; } = new List<Group>();
    public ICollection<Member> Memberships { get; set; } = new List<Member>();
}
