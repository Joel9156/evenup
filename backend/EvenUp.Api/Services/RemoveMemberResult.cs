namespace EvenUp.Api.Services;

public enum RemoveMemberError
{
    None,
    GroupNotFound,
    MemberNotFound,
    Forbidden,

    // Removing them would orphan historical expense data (who paid, who owed what) —
    // the group's expense records must stay attributable to a real member row.
    MemberHasExpenses,

    // A group must always have at least one sign-in member (the invariant the spec relies
    // on to guarantee someone can always manage the group) — this blocks removing the last one.
    LastSignedInMember,
}

public class RemoveMemberResult
{
    public bool Succeeded { get; private init; }
    public RemoveMemberError Error { get; private init; }

    public static RemoveMemberResult Ok() => new() { Succeeded = true, Error = RemoveMemberError.None };
    public static RemoveMemberResult Fail(RemoveMemberError error) => new() { Succeeded = false, Error = error };
}
