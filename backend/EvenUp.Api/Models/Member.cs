namespace EvenUp.Api.Models;

public class Member
{
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;

    // Null when the member is a guest (no account).
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public string DisplayName { get; set; } = string.Empty;
    public bool IsGuest { get; set; }
    public DateTime JoinedAt { get; set; }
}
