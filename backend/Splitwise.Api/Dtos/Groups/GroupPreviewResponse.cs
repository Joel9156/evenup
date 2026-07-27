namespace Splitwise.Api.Dtos.Groups;

// A member row visible from an invite preview, before joining. Member ids here are only
// ever guest placeholders (IsGuest is always true in this list — see GetGroupPreviewAsync) —
// exposing them lets a visitor say "that's me" and claim the existing row instead of a
// brand-new visitor always creating a duplicate for the same real person.
public class PreviewMemberResponse
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

// Shown before joining, from an invite link — deliberately minimal (no balances). GroupId
// itself isn't sensitive (same exposure as GET /api/groups/{id}), and the client needs it to
// actually call POST /api/groups/{id}/join afterward.
public class GroupPreviewResponse
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;

    // All current members, for display purposes.
    public List<string> MemberNames { get; set; } = [];

    // Subset of the above that are unclaimed guest placeholders — safe to offer as
    // "that's me" options, since claiming one can never take over an already-registered
    // person's identity.
    public List<PreviewMemberResponse> ClaimableMembers { get; set; } = [];
}
