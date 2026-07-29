using EvenUp.Api.Dtos.Groups;

namespace EvenUp.Api.Services;

public enum UpdateMemberError
{
    None,
    GroupNotFound,
    MemberNotFound,
    Forbidden,
}

public class UpdateMemberResult
{
    public bool Succeeded { get; private init; }
    public MemberResponse? Member { get; private init; }
    public UpdateMemberError Error { get; private init; }

    public static UpdateMemberResult Ok(MemberResponse member) => new() { Succeeded = true, Member = member, Error = UpdateMemberError.None };
    public static UpdateMemberResult Fail(UpdateMemberError error) => new() { Succeeded = false, Error = error };
}
