namespace Splitwise.Api.Services;

public enum AiChatError
{
    None,
    GroupNotFound,
    Forbidden,
}

public class AiChatResult<T>
{
    public bool Succeeded { get; private init; }
    public T? Value { get; private init; }
    public AiChatError Error { get; private init; }

    public static AiChatResult<T> Ok(T value) => new() { Succeeded = true, Value = value, Error = AiChatError.None };
    public static AiChatResult<T> Fail(AiChatError error) => new() { Succeeded = false, Error = error };
}
