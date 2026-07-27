namespace Splitwise.Api.Dtos.Groups;

// Shown before joining, from an invite link — deliberately minimal (no ids, no balances).
public class GroupPreviewResponse
{
    public string GroupName { get; set; } = string.Empty;
    public List<string> MemberNames { get; set; } = [];
}
