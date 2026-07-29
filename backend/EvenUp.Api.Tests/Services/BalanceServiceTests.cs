using Microsoft.EntityFrameworkCore;
using EvenUp.Api.Data;
using EvenUp.Api.Models;
using EvenUp.Api.Services;
using Xunit;

namespace EvenUp.Api.Tests.Services;

public class BalanceServiceTests
{
    private static EvenUpDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<EvenUpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EvenUpDbContext(options);
    }

    private record SeededGroup(Group Group, Member Alice, Member Bob, Member Carol);

    private static async Task<SeededGroup> SeedGroupWithDinnerExpenseAsync(EvenUpDbContext db)
    {
        var creatorUser = new User { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid()}@example.com", PasswordHash = "x", DisplayName = "Alice", CreatedAt = DateTime.UtcNow };
        db.Users.Add(creatorUser);

        var group = new Group { Id = Guid.NewGuid(), Name = "Trip", InviteCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), CreatedByUserId = creatorUser.Id, CreatedAt = DateTime.UtcNow };
        db.Groups.Add(group);

        var alice = new Member { Id = Guid.NewGuid(), GroupId = group.Id, UserId = creatorUser.Id, DisplayName = "Alice", IsGuest = false, JoinedAt = DateTime.UtcNow };
        var bob = new Member { Id = Guid.NewGuid(), GroupId = group.Id, UserId = null, DisplayName = "Bob", IsGuest = true, JoinedAt = DateTime.UtcNow };
        var carol = new Member { Id = Guid.NewGuid(), GroupId = group.Id, UserId = null, DisplayName = "Carol", IsGuest = true, JoinedAt = DateTime.UtcNow };
        db.Members.AddRange(alice, bob, carol);

        // Alice paid $90 for dinner, split evenly three ways ($30 each).
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            PaidByMemberId = alice.Id,
            CreatedByMemberId = alice.Id,
            Description = "Dinner",
            TotalAmount = 90m,
            CreatedAt = DateTime.UtcNow,
        };
        db.Expenses.Add(expense);
        db.ExpenseShares.AddRange(
            new ExpenseShare { Id = Guid.NewGuid(), ExpenseId = expense.Id, MemberId = alice.Id, ShareAmount = 30m },
            new ExpenseShare { Id = Guid.NewGuid(), ExpenseId = expense.Id, MemberId = bob.Id, ShareAmount = 30m },
            new ExpenseShare { Id = Guid.NewGuid(), ExpenseId = expense.Id, MemberId = carol.Id, ShareAmount = 30m }
        );

        await db.SaveChangesAsync();

        return new SeededGroup(group, alice, bob, carol);
    }

    [Fact]
    public async Task GetBalancesAsync_UnknownGroup_ReturnsNull()
    {
        using var db = CreateDb();
        var sut = new BalanceService(db, new SettlementCalculator());

        var result = await sut.GetBalancesAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetBalancesAsync_ComputesNetBalancesFromExpensesAndShares()
    {
        using var db = CreateDb();
        var seed = await SeedGroupWithDinnerExpenseAsync(db);
        var sut = new BalanceService(db, new SettlementCalculator());

        var result = await sut.GetBalancesAsync(seed.Group.Id);

        Assert.NotNull(result);
        Assert.Equal(60m, result!.NetBalances.Single(m => m.MemberId == seed.Alice.Id).NetBalance); // paid 90, owes 30
        Assert.Equal(-30m, result.NetBalances.Single(m => m.MemberId == seed.Bob.Id).NetBalance);
        Assert.Equal(-30m, result.NetBalances.Single(m => m.MemberId == seed.Carol.Id).NetBalance);
    }

    [Fact]
    public async Task GetBalancesAsync_SuggestedTransactions_RouteThroughTheCreditor()
    {
        using var db = CreateDb();
        var seed = await SeedGroupWithDinnerExpenseAsync(db);
        var sut = new BalanceService(db, new SettlementCalculator());

        var result = await sut.GetBalancesAsync(seed.Group.Id);

        Assert.Equal(2, result!.SuggestedTransactions.Count);
        Assert.All(result.SuggestedTransactions, t => Assert.Equal("Alice", t.ToDisplayName));
        Assert.Equal(60m, result.SuggestedTransactions.Sum(t => t.Amount));
    }

    [Fact]
    public async Task GetBalancesAsync_MemberWithNoExpenseActivity_HasZeroBalance()
    {
        using var db = CreateDb();
        var seed = await SeedGroupWithDinnerExpenseAsync(db);

        // A fourth member joins after the dinner was already logged — never appears in any expense.
        var dave = new Member { Id = Guid.NewGuid(), GroupId = seed.Group.Id, UserId = null, DisplayName = "Dave", IsGuest = true, JoinedAt = DateTime.UtcNow };
        db.Members.Add(dave);
        await db.SaveChangesAsync();

        var sut = new BalanceService(db, new SettlementCalculator());
        var result = await sut.GetBalancesAsync(seed.Group.Id);

        Assert.Equal(0m, result!.NetBalances.Single(m => m.MemberId == dave.Id).NetBalance);
    }

    [Fact]
    public async Task SettleAsync_UnknownGroup_ReturnsNull()
    {
        using var db = CreateDb();
        var sut = new BalanceService(db, new SettlementCalculator());

        var result = await sut.SettleAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task SettleAsync_PersistsASettlementSnapshot()
    {
        using var db = CreateDb();
        var seed = await SeedGroupWithDinnerExpenseAsync(db);
        var sut = new BalanceService(db, new SettlementCalculator());

        var result = await sut.SettleAsync(seed.Group.Id);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Transactions.Count);

        var stored = await db.Settlements.SingleAsync(s => s.GroupId == seed.Group.Id);
        Assert.Equal(result.SettlementId, stored.Id);
        Assert.Contains("Alice", stored.SnapshotJson); // display names captured at settlement time
        Assert.Contains("30", stored.SnapshotJson);
    }
}
