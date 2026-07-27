namespace Splitwise.Api.Dtos.Groups;

public class MemberResponse
{
    public Guid Id { get; set; }

    // Null for guests. Lets a signed-in client match "which member row is me" against its
    // own user id — otherwise there'd be no way to tell without guessing by display name.
    public Guid? UserId { get; set; }

    public string DisplayName { get; set; } = string.Empty;
    public bool IsGuest { get; set; }
    public DateTime JoinedAt { get; set; }
}
