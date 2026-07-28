using Microsoft.EntityFrameworkCore;
using Splitwise.Api.Models;
using Splitwise.Api.Services;

namespace Splitwise.Api.Data;

// Populates one browsable demo group so a recruiter can see real activity (varied expenses,
// non-zero balances, a settlement they can trigger themselves) without registering an
// account — GET /api/groups/{id} and the invite-preview endpoint are both public routes.
// Idempotent and safe to run on every startup: it checks for the fixed invite code first and
// does nothing if the demo group already exists, so redeploys don't pile up duplicates.
public static class DemoDataSeeder
{
    public const string DemoInviteCode = "DEMOTRIP";

    public static async Task SeedAsync(SplitwiseDbContext db, IPasswordHasher passwordHasher, IAccountEncryptionService accountEncryption, CancellationToken ct = default)
    {
        if (await db.Groups.AnyAsync(g => g.InviteCode == DemoInviteCode, ct))
        {
            return;
        }

        var host = new User
        {
            Id = Guid.NewGuid(),
            Email = "demo-host@splitwise.example",
            PasswordHash = passwordHasher.Hash(Guid.NewGuid().ToString()), // not a real login path; just needs to satisfy the NOT NULL column
            DisplayName = "Alex",
            BankName = "Kiwibank",
            AccountNumberEncrypted = accountEncryption.Encrypt("38-1234-5678900-00"),
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(host);

        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = "Bali Trip 2026",
            InviteCode = DemoInviteCode,
            CreatedByUserId = host.Id,
            CreatedAt = DateTime.UtcNow,
        };
        db.Groups.Add(group);

        Member NewMember(string name, bool isGuest, Guid? userId = null) => new()
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            UserId = userId,
            DisplayName = name,
            IsGuest = isGuest,
            JoinedAt = DateTime.UtcNow,
        };

        var alex = NewMember("Alex", isGuest: false, userId: host.Id);
        var sam = NewMember("Sam", isGuest: true);
        var jamie = NewMember("Jamie", isGuest: true);
        var taylor = NewMember("Taylor", isGuest: true);
        db.Members.AddRange(alex, sam, jamie, taylor);

        Expense NewExpense(string description, decimal total, Member paidBy, DateTime createdAt, params (Member Member, decimal Amount)[] shares) => new()
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            PaidByMemberId = paidBy.Id,
            CreatedByMemberId = alex.Id,
            Description = description,
            TotalAmount = total,
            CreatedAt = createdAt,
            Shares = shares.Select(s => new ExpenseShare { Id = Guid.NewGuid(), MemberId = s.Member.Id, ShareAmount = s.Amount }).ToList(),
        };

        var now = DateTime.UtcNow;
        db.Expenses.AddRange(
            NewExpense("Villa (3 nights)", 450m, alex, now.AddDays(-4),
                (alex, 112.50m), (sam, 112.50m), (jamie, 112.50m), (taylor, 112.50m)),
            NewExpense("Groceries", 86m, sam, now.AddDays(-3),
                (alex, 21.50m), (sam, 21.50m), (jamie, 21.50m), (taylor, 21.50m)),
            NewExpense("Scooter rental", 60m, jamie, now.AddDays(-2),
                (jamie, 30m), (taylor, 30m)), // just the two of them, not the whole group
            NewExpense("Dinner at beach club", 132m, alex, now.AddDays(-1),
                (alex, 33m), (sam, 33m), (jamie, 33m), (taylor, 33m)));

        await db.SaveChangesAsync(ct);
    }
}
