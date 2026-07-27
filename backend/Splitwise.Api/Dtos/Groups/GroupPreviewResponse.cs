namespace Splitwise.Api.Dtos.Groups;

// Shown before joining, from an invite link — deliberately minimal (no balances, no member
// ids). GroupId itself isn't sensitive (same exposure as GET /api/groups/{id}), and the
// client needs it to actually call POST /api/groups/{id}/join afterward.
public class GroupPreviewResponse
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public List<string> MemberNames { get; set; } = [];
}
