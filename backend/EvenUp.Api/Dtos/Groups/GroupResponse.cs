namespace EvenUp.Api.Dtos.Groups;

public class GroupResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<MemberResponse> Members { get; set; } = [];
}
