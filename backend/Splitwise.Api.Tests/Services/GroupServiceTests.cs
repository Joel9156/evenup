using Microsoft.EntityFrameworkCore;
using Splitwise.Api.Data;
using Splitwise.Api.Dtos.Groups;
using Splitwise.Api.Models;
using Splitwise.Api.Services;
using Xunit;

namespace Splitwise.Api.Tests.Services;

public class GroupServiceTests
{
    private static SplitwiseDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SplitwiseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SplitwiseDbContext(options);
    }

    private static async Task<User> SeedUserAsync(SplitwiseDbContext db, string displayName)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@example.com",
            PasswordHash = "irrelevant-for-these-tests",
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task CreateGroupAsync_AddsCreatorAsFirstNonGuestMember()
    {
        using var db = CreateDb();
        var sut = new GroupService(db, new InviteCodeGenerator());
        var creator = await SeedUserAsync(db, "Alice");

        var group = await sut.CreateGroupAsync(creator.Id, new CreateGroupRequest { Name = "Trip to Queenstown" });

        var member = Assert.Single(group.Members);
        Assert.Equal("Alice", member.DisplayName);
        Assert.False(member.IsGuest);
        Assert.Equal(8, group.InviteCode.Length);
    }

    [Fact]
    public async Task JoinGroupAsync_AsGuest_CreatesGuestMemberWithNoUserId()
    {
        using var db = CreateDb();
        var sut = new GroupService(db, new InviteCodeGenerator());
        var creator = await SeedUserAsync(db, "Alice");
        var group = await sut.CreateGroupAsync(creator.Id, new CreateGroupRequest { Name = "Flatmates" });

        var result = await sut.JoinGroupAsync(group.Id, signedInUserId: null, new JoinGroupRequest { DisplayName = "Bob (guest)" });

        Assert.NotNull(result);
        Assert.True(result!.IsGuest);

        var storedMember = await db.Members.FindAsync(result.MemberId);
        Assert.Null(storedMember!.UserId);
    }

    [Fact]
    public async Task JoinGroupAsync_AsSignedInUser_CreatesNonGuestMemberLinkedToUser()
    {
        using var db = CreateDb();
        var sut = new GroupService(db, new InviteCodeGenerator());
        var creator = await SeedUserAsync(db, "Alice");
        var joiner = await SeedUserAsync(db, "Carol");
        var group = await sut.CreateGroupAsync(creator.Id, new CreateGroupRequest { Name = "Flatmates" });

        var result = await sut.JoinGroupAsync(group.Id, joiner.Id, new JoinGroupRequest { DisplayName = "Carol" });

        Assert.NotNull(result);
        Assert.False(result!.IsGuest);

        var storedMember = await db.Members.FindAsync(result.MemberId);
        Assert.Equal(joiner.Id, storedMember!.UserId);
    }

    [Fact]
    public async Task JoinGroupAsync_SignedInUserJoiningTwice_ReturnsExistingMembershipInstead()
    {
        using var db = CreateDb();
        var sut = new GroupService(db, new InviteCodeGenerator());
        var creator = await SeedUserAsync(db, "Alice");
        var joiner = await SeedUserAsync(db, "Carol");
        var group = await sut.CreateGroupAsync(creator.Id, new CreateGroupRequest { Name = "Flatmates" });

        var first = await sut.JoinGroupAsync(group.Id, joiner.Id, new JoinGroupRequest { DisplayName = "Carol" });
        var second = await sut.JoinGroupAsync(group.Id, joiner.Id, new JoinGroupRequest { DisplayName = "Carol" });

        Assert.Equal(first!.MemberId, second!.MemberId);
        Assert.Equal(2, (await db.Members.Where(m => m.GroupId == group.Id).ToListAsync()).Count); // creator + Carol, not 3
    }

    [Fact]
    public async Task JoinGroupAsync_UnknownGroup_ReturnsNull()
    {
        using var db = CreateDb();
        var sut = new GroupService(db, new InviteCodeGenerator());

        var result = await sut.JoinGroupAsync(Guid.NewGuid(), null, new JoinGroupRequest { DisplayName = "Nobody" });

        Assert.Null(result);
    }

    [Fact]
    public async Task GetGroupPreviewAsync_UnknownInviteCode_ReturnsNull()
    {
        using var db = CreateDb();
        var sut = new GroupService(db, new InviteCodeGenerator());

        var result = await sut.GetGroupPreviewAsync("NOSUCH1");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetGroupPreviewAsync_KnownInviteCode_ReturnsGroupNameAndMemberNames()
    {
        using var db = CreateDb();
        var sut = new GroupService(db, new InviteCodeGenerator());
        var creator = await SeedUserAsync(db, "Alice");
        var group = await sut.CreateGroupAsync(creator.Id, new CreateGroupRequest { Name = "Ski Trip" });

        var preview = await sut.GetGroupPreviewAsync(group.InviteCode);

        Assert.NotNull(preview);
        Assert.Equal("Ski Trip", preview!.GroupName);
        Assert.Contains("Alice", preview.MemberNames);
    }
}
