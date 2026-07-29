using Microsoft.EntityFrameworkCore;
using EvenUp.Api.Data;
using EvenUp.Api.Dtos.Expenses;
using EvenUp.Api.Models;

namespace EvenUp.Api.Services;

public class ExpenseService(EvenUpDbContext db) : IExpenseService
{
    // Rounding tolerance for comparing decimal sums (avoids false mismatches from cent-level rounding).
    private const decimal AmountTolerance = 0.01m;

    public async Task<ExpenseResult<ExpenseResponse>> CreateExpenseAsync(Guid groupId, CreateExpenseRequest request, CancellationToken ct = default)
    {
        var group = await db.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null)
        {
            return ExpenseResult<ExpenseResponse>.Fail(ExpenseError.GroupNotFound);
        }

        var memberIds = group.Members.Select(m => m.Id).ToHashSet();
        if (!memberIds.Contains(request.PaidByMemberId) || !memberIds.Contains(request.CreatedByMemberId))
        {
            return ExpenseResult<ExpenseResponse>.Fail(ExpenseError.InvalidRequest);
        }

        var shareError = ValidateShares(request.TotalAmount, memberIds, request.Shares);
        if (shareError != ExpenseError.None)
        {
            return ExpenseResult<ExpenseResponse>.Fail(shareError);
        }

        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            PaidByMemberId = request.PaidByMemberId,
            CreatedByMemberId = request.CreatedByMemberId,
            Description = request.Description.Trim(),
            TotalAmount = request.TotalAmount,
            CreatedAt = DateTime.UtcNow,
        };

        expense.Shares = request.Shares.Select(s => new ExpenseShare
        {
            Id = Guid.NewGuid(),
            ExpenseId = expense.Id,
            MemberId = s.MemberId,
            ShareAmount = s.Amount,
        }).ToList();

        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);

        return ExpenseResult<ExpenseResponse>.Ok(ToExpenseResponse(expense, group.Members));
    }

    public async Task<List<ExpenseResponse>?> GetExpensesAsync(Guid groupId, CancellationToken ct = default)
    {
        var group = await db.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null)
        {
            return null;
        }

        var expenses = await db.Expenses
            .Include(e => e.Shares)
            .Where(e => e.GroupId == groupId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);

        return expenses.Select(e => ToExpenseResponse(e, group.Members)).ToList();
    }

    public async Task<ExpenseResult<ExpenseResponse>> UpdateExpenseAsync(Guid expenseId, Guid requestingUserId, UpdateExpenseRequest request, CancellationToken ct = default)
    {
        var expense = await db.Expenses
            .Include(e => e.Group).ThenInclude(g => g.Members)
            .Include(e => e.CreatedByMember)
            .Include(e => e.Shares)
            .FirstOrDefaultAsync(e => e.Id == expenseId, ct);

        if (expense is null)
        {
            return ExpenseResult<ExpenseResponse>.Fail(ExpenseError.ExpenseNotFound);
        }

        // Guests can never pass this check: their Member.UserId is null, which never equals
        // an authenticated user's id. That's the enforcement point for "guests can't edit."
        if (expense.CreatedByMember.UserId != requestingUserId)
        {
            return ExpenseResult<ExpenseResponse>.Fail(ExpenseError.Forbidden);
        }

        var memberIds = expense.Group.Members.Select(m => m.Id).ToHashSet();
        if (!memberIds.Contains(request.PaidByMemberId))
        {
            return ExpenseResult<ExpenseResponse>.Fail(ExpenseError.InvalidRequest);
        }

        var shareError = ValidateShares(request.TotalAmount, memberIds, request.Shares);
        if (shareError != ExpenseError.None)
        {
            return ExpenseResult<ExpenseResponse>.Fail(shareError);
        }

        expense.Description = request.Description.Trim();
        expense.TotalAmount = request.TotalAmount;
        expense.PaidByMemberId = request.PaidByMemberId;
        expense.UpdatedAt = DateTime.UtcNow;

        // Reassigning expense.Shares before saving (instead of after) would make EF Core's
        // change tracker try to orphan-fixup the just-removed entities too, causing a
        // conflicting delete/update on the same rows — so the new list is only attached to
        // the navigation property after the removal has actually been saved.
        db.ExpenseShares.RemoveRange(expense.Shares);

        var newShares = request.Shares.Select(s => new ExpenseShare
        {
            Id = Guid.NewGuid(),
            ExpenseId = expense.Id,
            MemberId = s.MemberId,
            ShareAmount = s.Amount,
        }).ToList();
        db.ExpenseShares.AddRange(newShares);

        await db.SaveChangesAsync(ct);
        expense.Shares = newShares;

        return ExpenseResult<ExpenseResponse>.Ok(ToExpenseResponse(expense, expense.Group.Members));
    }

    public async Task<ExpenseResult<bool>> DeleteExpenseAsync(Guid expenseId, Guid requestingUserId, CancellationToken ct = default)
    {
        var expense = await db.Expenses
            .Include(e => e.CreatedByMember)
            .FirstOrDefaultAsync(e => e.Id == expenseId, ct);

        if (expense is null)
        {
            return ExpenseResult<bool>.Fail(ExpenseError.ExpenseNotFound);
        }

        if (expense.CreatedByMember.UserId != requestingUserId)
        {
            return ExpenseResult<bool>.Fail(ExpenseError.Forbidden);
        }

        db.Expenses.Remove(expense); // ExpenseShares cascade-delete via the FK configured in EvenUpDbContext.
        await db.SaveChangesAsync(ct);

        return ExpenseResult<bool>.Ok(true);
    }

    private static ExpenseError ValidateShares(decimal totalAmount, HashSet<Guid> validMemberIds, List<ExpenseShareRequest> shares)
    {
        if (shares.Count == 0)
        {
            return ExpenseError.InvalidRequest;
        }

        var shareMemberIds = shares.Select(s => s.MemberId).ToList();
        if (shareMemberIds.Distinct().Count() != shareMemberIds.Count)
        {
            return ExpenseError.InvalidRequest; // duplicate member in shares
        }

        if (shares.Any(s => s.Amount <= 0 || !validMemberIds.Contains(s.MemberId)))
        {
            return ExpenseError.InvalidRequest;
        }

        var shareSum = shares.Sum(s => s.Amount);
        if (Math.Abs(shareSum - totalAmount) > AmountTolerance)
        {
            return ExpenseError.ShareSumMismatch;
        }

        return ExpenseError.None;
    }

    private static ExpenseResponse ToExpenseResponse(Expense expense, IEnumerable<Member> groupMembers)
    {
        var membersById = groupMembers.ToDictionary(m => m.Id, m => m.DisplayName);

        return new ExpenseResponse
        {
            Id = expense.Id,
            GroupId = expense.GroupId,
            Description = expense.Description,
            TotalAmount = expense.TotalAmount,
            PaidByMemberId = expense.PaidByMemberId,
            PaidByDisplayName = membersById.GetValueOrDefault(expense.PaidByMemberId, "Unknown"),
            CreatedByMemberId = expense.CreatedByMemberId,
            CreatedAt = expense.CreatedAt,
            UpdatedAt = expense.UpdatedAt,
            Shares = expense.Shares.Select(s => new ExpenseShareResponse
            {
                MemberId = s.MemberId,
                MemberDisplayName = membersById.GetValueOrDefault(s.MemberId, "Unknown"),
                Amount = s.ShareAmount,
            }).ToList(),
        };
    }
}
