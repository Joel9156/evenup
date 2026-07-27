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

    [Fact]
    public async Task GetMyGroupsAsync_ReturnsOnlyGroupsTheUserIsAMemberOf()
    {
        using var db = CreateDb();
        var sut = new GroupService(db, new InviteCodeGenerator());
        var alice = await SeedUserAsync(db, "Alice");
        var bob = await SeedUserAsync(db, "Bob");

        var aliceGroup = await sut.CreateGroupAsync(alice.Id, new CreateGroupRequest { Name = "Alice's Trip" });
        await sut.CreateGroupAsync(bob.Id, new CreateGroupRequest { Name = "Bob's Trip" }); // Alice isn't in this one

        var result = await sut.GetMyGroupsAsync(alice.Id);

        var group = Assert.Single(result);
        Assert.Equal(aliceGroup.Id, group.Id);
    }

    [Fact]
    public async Task GetMyGroupsAsync_IncludesGroupsJoinedViaInvite_NotOnlyCreated()
    {
        using var db = CreateDb();
        var sut = new GroupService(db, new InviteCodeGenerator());
        var alice = await SeedUserAsync(db, "Alice");
        var bob = await SeedUserAsync(db, "Bob");

        var bobsGroup = await sut.CreateGroupAsync(bob.Id, new CreateGroupRequest { Name = "Bob's Trip" });
        await sut.JoinGroupAsync(bobsGroup.Id, alice.Id, new JoinGroupRequest { DisplayName = "Alice" });

        var result = await sut.GetMyGroupsAsync(alice.Id);

        var group = Assert.Single(result);
        Assert.Equal(bobsGroup.Id, group.Id);
    }

    [Fact]
    public async Task GetMyGroupsAsync_UserInNoGroups_ReturnsEmptyList()
    {
        using var db = CreateDb();
        var sut = new GroupService(db, new InviteCodeGenerator());
        var alice = await SeedUserAsync(db, "Alice");

        var result = await sut.GetMyGroupsAsync(alice.Id);

        Assert.Empty(result);
    }

    [Fact]
    public async Task AddMemberAsync_ByExistingMember_CreatesGuestPlaceholderWithNoUserId()
    {
        using var db = CreateDb();
        var sut = new GroupService(db, new InviteCodeGenerator());
        var alice = await SeedUserAsync(db, "Alice");
        var group = await sut.CreateGroupAsync(alice.Id, new CreateGroupRequest { Name = "Solo-tracked Trip" });

        var result = await sut.AddMemberAsync(group.Id, alice.Id, new AddMemberRequest { DisplayName = "Bob" });

        Assert.True(result.Succeeded);
        Assert.Equal("Bob", result.Member!.DisplayName);
        Assert.True(result.Member.IsGuest);
        Assert.Null(result.Member.UserId);
    }

    [Fact]
    public async Task AddMemberAsync_ByNonMember_ReturnsForbidden()
    {
        using var db = CreateDb();
        var sut = new GroupService(db, new InviteCodeGenerator());
        var alice = await SeedUserAsync(db, "Alice");
        var stranger = await SeedUserAsync(db, "Stranger");
        var group = await sut.CreateGroupAsync(alice.Id, new CreateGroupRequest { Name = "Trip" });

        var result = await sut.AddMemberAsync(group.Id, stranger.Id, new AddMemberRequest { DisplayName = "Bob" });

        Assert.False(result.Succeeded);
        Assert.Equal(AddMemberError.Forbidden, result.Error);
    }

    [Fact]
    public async Task AddMemberAsync_UnknownGroup_ReturnsGroupNotFound()
    {
        using var db = CreateDb();
        var sut = new GroupService(db, new InviteCodeGenerator());
        var alice = await SeedUserAsync(db, "Alice");

        var result = await sut.AddMemberAsync(Guid.NewGuid(), alice.Id, new AddMemberRequest { DisplayName = "Bob" });

        Assert.False(result.Succeeded);
        Assert.Equal(AddMemberError.GroupNotFound, result.Error);
    }

    [Fact]
    public async Task GetGroupPreviewAsync_ClaimableMembers_OnlyIncludesUnclaimedGuests()
    {
        using var db = CreateDb();
        var sut = new GroupService(db, new InviteCodeGenerator());
        var alice = await SeedUserAsync(db, "Alice");
        var group = await sut.CreateGroupAsync(alice.Id, new CreateGroupRequest { Name = "Trip" });
        await sut.AddMemberAsync(group.Id, alice.Id, new AddMemberRequest { DisplayName = "Bob" });

        var preview = await sut.GetGroupPreviewAsync(group.InviteCode);

        Assert.Equal(2, preview!.MemberNames.Count); // Alice + Bob both listed
        var claimable = Assert.Single(preview.ClaimableMembers); // only Bob (guest) is claimable, not Alice (signed-in)
        Assert.Equal("Bob", claimable.DisplayName);
    }

    [Fact]
    public async Task JoinGroupAsync_SignedInUserClaimsExistingPlaceholder_LinksItInsteadOfDuplicating()
    {
        using var db = CreateDb();
        var sut = new GroupService(db, new InviteCodeGenerator());
        var alice = await SeedUserAsync(db, "Alice");
        var bobUser = await SeedUserAsync(db, "Bob");
        var group = await sut.CreateGroupAsync(alice.Id, new CreateGroupRequest { Name = "Trip" });
        var placeholder = await sut.AddMemberAsync(group.Id, alice.Id, new AddMemberRequest { DisplayName = "Bob" });

        var result = await sut.JoinGroupAsync(group.Id, bobUser.Id, new JoinGroupRequest
        {
            DisplayName = "Bob",
            ExistingMemberId = placeholder.Member!.Id,
        });

        Assert.NotNull(result);
        Assert.Equal(placeholder.Member.Id, result!.MemberId); // same row, not a new one
        Assert.False(result.IsGuest);

        var updated = await db.Members.FindAsync(placeholder.Member.Id);
        Assert.Equal(bobUser.Id, updated!.UserId);
        Assert.False(updated.IsGuest);

        var groupAfter = await sut.GetGroupAsync(group.Id);
        Assert.Equal(2, groupAfter!.Members.Count); // still just Alice + (now-claimed) Bob, no duplicate
    }

    [Fact]
    public async Task JoinGroupAsync_ExistingMemberIdAlreadyClaimed_FallsBackToCreatingNewMember()
    {
        using var db = CreateDb();
        var sut = new GroupService(db, new InviteCodeGenerator());
        var alice = await SeedUserAsync(db, "Alice");
        var group = await sut.CreateGroupAsync(alice.Id, new CreateGroupRequest { Name = "Trip" });

        // Alice's own membership is already claimed (UserId set) — trying to "claim" it
        // as someone else should be ignored, not hijack her account's member row.
        var carolUser = await SeedUserAsync(db, "Carol");
        var result = await sut.JoinGroupAsync(group.Id, carolUser.Id, new JoinGroupRequest
        {
            DisplayName = "Carol",
            ExistingMemberId = group.Members.Single().Id, // Alice's member id
        });

        Assert.NotNull(result);
        Assert.NotEqual(group.Members.Single().Id, result!.MemberId); // got a brand-new member instead
        Assert.Equal("Carol", result.DisplayName);
    }
}
