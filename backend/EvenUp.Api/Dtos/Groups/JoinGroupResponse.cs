namespace EvenUp.Api.Dtos.Groups;

public class JoinGroupResponse
{
    public Guid MemberId { get; set; }
    public Guid GroupId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsGuest { get; set; }
}
