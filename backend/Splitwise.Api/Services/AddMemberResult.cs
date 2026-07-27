using Splitwise.Api.Dtos.Groups;

namespace Splitwise.Api.Services;

public enum AddMemberError
{
    None,
    GroupNotFound,
    Forbidden,
}

public class AddMemberResult
{
    public bool Succeeded { get; private init; }
    public MemberResponse? Member { get; private init; }
    public AddMemberError Error { get; private init; }

    public static AddMemberResult Ok(MemberResponse member) => new() { Succeeded = true, Member = member, Error = AddMemberError.None };
    public static AddMemberResult Fail(AddMemberError error) => new() { Succeeded = false, Error = error };
}
