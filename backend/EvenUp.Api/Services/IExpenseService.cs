using EvenUp.Api.Dtos.Expenses;

namespace EvenUp.Api.Services;

public interface IExpenseService
{
    Task<ExpenseResult<ExpenseResponse>> CreateExpenseAsync(Guid groupId, CreateExpenseRequest request, CancellationToken ct = default);
    Task<List<ExpenseResponse>?> GetExpensesAsync(Guid groupId, CancellationToken ct = default);
    Task<ExpenseResult<ExpenseResponse>> UpdateExpenseAsync(Guid expenseId, Guid requestingUserId, UpdateExpenseRequest request, CancellationToken ct = default);
    Task<ExpenseResult<bool>> DeleteExpenseAsync(Guid expenseId, Guid requestingUserId, CancellationToken ct = default);
}
