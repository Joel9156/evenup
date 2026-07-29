
namespace EvenUp.Api.Services;

public enum ExpenseError
{
    None,
    GroupNotFound,
    ExpenseNotFound,
    InvalidRequest,
    ShareSumMismatch,
    Forbidden,
}

public class ExpenseResult<T>
{
    public bool Succeeded { get; private init; }
    public T? Value { get; private init; }
    public ExpenseError Error { get; private init; }

    public static ExpenseResult<T> Ok(T value) => new() { Succeeded = true, Value = value, Error = ExpenseError.None };
    public static ExpenseResult<T> Fail(ExpenseError error) => new() { Succeeded = false, Error = error };
}
