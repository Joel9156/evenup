using Microsoft.EntityFrameworkCore;
using Splitwise.Api.Data;
using Splitwise.Api.Dtos.Expenses;
using Splitwise.Api.Models;
using Splitwise.Api.Services;
using Xunit;

namespace Splitwise.Api.Tests.Services;

public class ExpenseServiceTests
{
    private static SplitwiseDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SplitwiseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SplitwiseDbContext(options);
    }

    private record SeededGroup(Group Group, User CreatorUser, Member CreatorMember, User OtherUser, Member OtherMember, Member GuestMember);

    // A group with three members: the creator (sign-in), another sign-in member, and a guest —
    // enough to exercise "created by sign-in member" vs "created by guest" edit/delete rules.
    private static async Task<SeededGroup> SeedGroupAsync(SplitwiseDbContext db)
    {
        var creatorUser = new User { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid()}@example.com", PasswordHash = "x", DisplayName = "Alice", CreatedAt = DateTime.UtcNow };
        var otherUser = new User { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid()}@example.com", PasswordHash = "x", DisplayName = "Carol", CreatedAt = DateTime.UtcNow };
        db.Users.AddRange(creatorUser, otherUser);

        var group = new Group { Id = Guid.NewGuid(), Name = "Trip", InviteCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), CreatedByUserId = creatorUser.Id, CreatedAt = DateTime.UtcNow };
        db.Groups.Add(group);

        var creatorMember = new Member { Id = Guid.NewGuid(), GroupId = group.Id, UserId = creatorUser.Id, DisplayName = "Alice", IsGuest = false, JoinedAt = DateTime.UtcNow };
        var otherMember = new Member { Id = Guid.NewGuid(), GroupId = group.Id, UserId = otherUser.Id, DisplayName = "Carol", IsGuest = false, JoinedAt = DateTime.UtcNow };
        var guestMember = new Member { Id = Guid.NewGuid(), GroupId = group.Id, UserId = null, DisplayName = "Bob (guest)", IsGuest = true, JoinedAt = DateTime.UtcNow };
        db.Members.AddRange(creatorMember, otherMember, guestMember);

        await db.SaveChangesAsync();

        return new SeededGroup(group, creatorUser, creatorMember, otherUser, otherMember, guestMember);
    }

    [Fact]
    public async Task CreateExpenseAsync_ValidRequest_Succeeds()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var sut = new ExpenseService(db);

        var result = await sut.CreateExpenseAsync(seed.Group.Id, new CreateExpenseRequest
        {
            Description = "Dinner",
            TotalAmount = 90,
            PaidByMemberId = seed.CreatorMember.Id,
            CreatedByMemberId = seed.CreatorMember.Id,
            Shares =
            [
                new ExpenseShareRequest { MemberId = seed.CreatorMember.Id, Amount = 30 },
                new ExpenseShareRequest { MemberId = seed.OtherMember.Id, Amount = 30 },
                new ExpenseShareRequest { MemberId = seed.GuestMember.Id, Amount = 30 },
            ],
        });

        Assert.True(result.Succeeded);
        Assert.Equal("Alice", result.Value!.PaidByDisplayName);
        Assert.Equal(3, result.Value.Shares.Count);
    }

    [Fact]
    public async Task CreateExpenseAsync_SharesDoNotMatchTotal_ReturnsShareSumMismatch()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var sut = new ExpenseService(db);

        var result = await sut.CreateExpenseAsync(seed.Group.Id, new CreateExpenseRequest
        {
            Description = "Dinner",
            TotalAmount = 90,
            PaidByMemberId = seed.CreatorMember.Id,
            CreatedByMemberId = seed.CreatorMember.Id,
            Shares =
            [
                new ExpenseShareRequest { MemberId = seed.CreatorMember.Id, Amount = 30 },
                new ExpenseShareRequest { MemberId = seed.OtherMember.Id, Amount = 30 },
            ], // only sums to 60, not 90
        });

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseError.ShareSumMismatch, result.Error);
    }

    [Fact]
    public async Task CreateExpenseAsync_MemberNotInGroup_ReturnsInvalidRequest()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var sut = new ExpenseService(db);

        var result = await sut.CreateExpenseAsync(seed.Group.Id, new CreateExpenseRequest
        {
            Description = "Dinner",
            TotalAmount = 30,
            PaidByMemberId = seed.CreatorMember.Id,
            CreatedByMemberId = seed.CreatorMember.Id,
            Shares = [new ExpenseShareRequest { MemberId = Guid.NewGuid(), Amount = 30 }], // not a member of this group
        });

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseError.InvalidRequest, result.Error);
    }

    [Fact]
    public async Task CreateExpenseAsync_UnknownGroup_ReturnsGroupNotFound()
    {
        using var db = CreateDb();
        var sut = new ExpenseService(db);

        var result = await sut.CreateExpenseAsync(Guid.NewGuid(), new CreateExpenseRequest
        {
            Description = "Dinner",
            TotalAmount = 30,
            PaidByMemberId = Guid.NewGuid(),
            CreatedByMemberId = Guid.NewGuid(),
            Shares = [new ExpenseShareRequest { MemberId = Guid.NewGuid(), Amount = 30 }],
        });

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseError.GroupNotFound, result.Error);
    }

    [Fact]
    public async Task UpdateExpenseAsync_ByCreatorMemberOwner_Succeeds()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var sut = new ExpenseService(db);

        var created = await sut.CreateExpenseAsync(seed.Group.Id, new CreateExpenseRequest
        {
            Description = "Dinner",
            TotalAmount = 60,
            PaidByMemberId = seed.CreatorMember.Id,
            CreatedByMemberId = seed.CreatorMember.Id,
            Shares =
            [
                new ExpenseShareRequest { MemberId = seed.CreatorMember.Id, Amount = 30 },
                new ExpenseShareRequest { MemberId = seed.OtherMember.Id, Amount = 30 },
            ],
        });

        var updated = await sut.UpdateExpenseAsync(created.Value!.Id, seed.CreatorUser.Id, new UpdateExpenseRequest
        {
            Description = "Dinner (updated)",
            TotalAmount = 40,
            PaidByMemberId = seed.CreatorMember.Id,
            Shares = [new ExpenseShareRequest { MemberId = seed.CreatorMember.Id, Amount = 40 }],
        });

        Assert.True(updated.Succeeded);
        Assert.Equal("Dinner (updated)", updated.Value!.Description);
        Assert.Equal(40, updated.Value.TotalAmount);
        Assert.NotNull(updated.Value.UpdatedAt);
    }

    [Fact]
    public async Task UpdateExpenseAsync_ByDifferentSignedInUser_ReturnsForbidden()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var sut = new ExpenseService(db);

        var created = await sut.CreateExpenseAsync(seed.Group.Id, new CreateExpenseRequest
        {
            Description = "Dinner",
            TotalAmount = 30,
            PaidByMemberId = seed.CreatorMember.Id,
            CreatedByMemberId = seed.CreatorMember.Id,
            Shares = [new ExpenseShareRequest { MemberId = seed.CreatorMember.Id, Amount = 30 }],
        });

        // Carol didn't create this expense — only Alice (the creator member) may edit it.
        var result = await sut.UpdateExpenseAsync(created.Value!.Id, seed.OtherUser.Id, new UpdateExpenseRequest
        {
            Description = "Hijacked",
            TotalAmount = 30,
            PaidByMemberId = seed.CreatorMember.Id,
            Shares = [new ExpenseShareRequest { MemberId = seed.CreatorMember.Id, Amount = 30 }],
        });

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseError.Forbidden, result.Error);
    }

    [Fact]
    public async Task UpdateExpenseAsync_ExpenseCreatedByGuest_IsForbiddenForAnySignedInUser()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var sut = new ExpenseService(db);

        // The guest member itself created the expense (guests are allowed to add expenses).
        var created = await sut.CreateExpenseAsync(seed.Group.Id, new CreateExpenseRequest
        {
            Description = "Snacks",
            TotalAmount = 10,
            PaidByMemberId = seed.GuestMember.Id,
            CreatedByMemberId = seed.GuestMember.Id,
            Shares = [new ExpenseShareRequest { MemberId = seed.GuestMember.Id, Amount = 10 }],
        });

        // No signed-in user can ever match a guest member's (null) UserId, so this must always
        // be Forbidden — including for the group creator. This is the guest edit/delete lockout.
        var result = await sut.UpdateExpenseAsync(created.Value!.Id, seed.CreatorUser.Id, new UpdateExpenseRequest
        {
            Description = "Hijacked",
            TotalAmount = 10,
            PaidByMemberId = seed.GuestMember.Id,
            Shares = [new ExpenseShareRequest { MemberId = seed.GuestMember.Id, Amount = 10 }],
        });

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseError.Forbidden, result.Error);
    }

    [Fact]
    public async Task DeleteExpenseAsync_ByCreatorMemberOwner_RemovesExpenseAndShares()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var sut = new ExpenseService(db);

        var created = await sut.CreateExpenseAsync(seed.Group.Id, new CreateExpenseRequest
        {
            Description = "Dinner",
            TotalAmount = 30,
            PaidByMemberId = seed.CreatorMember.Id,
            CreatedByMemberId = seed.CreatorMember.Id,
            Shares = [new ExpenseShareRequest { MemberId = seed.CreatorMember.Id, Amount = 30 }],
        });

        var result = await sut.DeleteExpenseAsync(created.Value!.Id, seed.CreatorUser.Id);

        Assert.True(result.Succeeded);
        Assert.Empty(await db.Expenses.Where(e => e.Id == created.Value.Id).ToListAsync());
        Assert.Empty(await db.ExpenseShares.Where(s => s.ExpenseId == created.Value.Id).ToListAsync());
    }

    [Fact]
    public async Task DeleteExpenseAsync_ByDifferentUser_ReturnsForbidden()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var sut = new ExpenseService(db);

        var created = await sut.CreateExpenseAsync(seed.Group.Id, new CreateExpenseRequest
        {
            Description = "Dinner",
            TotalAmount = 30,
            PaidByMemberId = seed.CreatorMember.Id,
            CreatedByMemberId = seed.CreatorMember.Id,
            Shares = [new ExpenseShareRequest { MemberId = seed.CreatorMember.Id, Amount = 30 }],
        });

        var result = await sut.DeleteExpenseAsync(created.Value!.Id, seed.OtherUser.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(ExpenseError.Forbidden, result.Error);
        Assert.NotEmpty(await db.Expenses.Where(e => e.Id == created.Value.Id).ToListAsync()); // untouched
    }

    [Fact]
    public async Task GetExpensesAsync_UnknownGroup_ReturnsNull()
    {
        using var db = CreateDb();
        var sut = new ExpenseService(db);

        var result = await sut.GetExpensesAsync(Guid.NewGuid());

        Assert.Null(result);
    }
}
