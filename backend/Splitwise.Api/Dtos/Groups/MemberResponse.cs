namespace Splitwise.Api.Dtos.Groups;

public class MemberResponse
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsGuest { get; set; }
    public DateTime JoinedAt { get; set; }
}
