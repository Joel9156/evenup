namespace EvenUp.Api.Services;

public enum AuthError
{
    None,
    EmailAlreadyExists,
    InvalidCredentials,
}

public class AuthResult<T>
{
    public bool Succeeded { get; private init; }
    public T? Value { get; private init; }
    public AuthError Error { get; private init; }

    public static AuthResult<T> Ok(T value) => new() { Succeeded = true, Value = value, Error = AuthError.None };
    public static AuthResult<T> Fail(AuthError error) => new() { Succeeded = false, Error = error };
}
