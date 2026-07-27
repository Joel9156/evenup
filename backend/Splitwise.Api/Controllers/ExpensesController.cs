using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Splitwise.Api.Dtos.Expenses;
using Splitwise.Api.Extensions;
using Splitwise.Api.Services;

namespace Splitwise.Api.Controllers;

// Two route shapes, matching the spec's endpoint table: create/list are nested under the
// group (guests can call them, no auth), edit/delete are top-level and require the caller
// to be the sign-in member who created the expense.
[ApiController]
public class ExpensesController(IExpenseService expenseService) : ControllerBase
{
    [HttpPost("api/groups/{groupId:guid}/expenses")]
    public async Task<ActionResult<ExpenseResponse>> Create(Guid groupId, CreateExpenseRequest request, CancellationToken ct)
    {
        var result = await expenseService.CreateExpenseAsync(groupId, request, ct);
        return ToActionResult(result);
    }

    [HttpGet("api/groups/{groupId:guid}/expenses")]
    public async Task<ActionResult<List<ExpenseResponse>>> List(Guid groupId, CancellationToken ct)
    {
        var expenses = await expenseService.GetExpensesAsync(groupId, ct);
        return expenses is null ? NotFound() : Ok(expenses);
    }

    [Authorize]
    [HttpPut("api/expenses/{id:guid}")]
    public async Task<ActionResult<ExpenseResponse>> Update(Guid id, UpdateExpenseRequest request, CancellationToken ct)
    {
        var result = await expenseService.UpdateExpenseAsync(id, User.GetUserId(), request, ct);
        return ToActionResult(result);
    }

    [Authorize]
    [HttpDelete("api/expenses/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await expenseService.DeleteExpenseAsync(id, User.GetUserId(), ct);
        return result.Error switch
        {
            ExpenseError.None => NoContent(),
            ExpenseError.ExpenseNotFound => NotFound(new { message = "Expense not found." }),
            ExpenseError.Forbidden => Forbid(),
            _ => BadRequest(new { message = "Could not delete this expense." }),
        };
    }

    private ActionResult<ExpenseResponse> ToActionResult(ExpenseResult<ExpenseResponse> result) => result.Error switch
    {
        ExpenseError.None => Ok(result.Value),
        ExpenseError.GroupNotFound => NotFound(new { message = "Group not found." }),
        ExpenseError.ExpenseNotFound => NotFound(new { message = "Expense not found." }),
        ExpenseError.Forbidden => Forbid(),
        ExpenseError.ShareSumMismatch => BadRequest(new { message = "Shares must add up to the total amount." }),
        ExpenseError.InvalidRequest => BadRequest(new { message = "One or more members are invalid, or amounts are not positive." }),
        _ => BadRequest(),
    };
}
