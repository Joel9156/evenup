using Microsoft.EntityFrameworkCore;
using Splitwise.Api.Data;
using Splitwise.Api.Dtos.AiChat;
using Splitwise.Api.Models;
using Splitwise.Api.Services;
using Xunit;

namespace Splitwise.Api.Tests.Services;

public class AiChatServiceTests
{
    // A stand-in for the real OpenAI call — returns whatever result the test hands it, so
    // AiChatService's name-resolution and validation logic can be tested without network
    // access or a real API key.
    private class FakeAiExpenseParser(LogExpenseToolResult result) : IAiExpenseParser
    {
        public Task<LogExpenseToolResult> ParseAsync(IReadOnlyList<string> memberNames, IReadOnlyList<AiChatMessageDto> conversation, CancellationToken ct = default)
            => Task.FromResult(result);
    }

    private static SplitwiseDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SplitwiseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SplitwiseDbContext(options);
    }

    private record SeededGroup(Group Group, User AliceUser, Member Alice, Member Bob);

    private static async Task<SeededGroup> SeedGroupAsync(SplitwiseDbContext db)
    {
        var aliceUser = new User { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid()}@example.com", PasswordHash = "x", DisplayName = "Alice", CreatedAt = DateTime.UtcNow };
        db.Users.Add(aliceUser);
        var group = new Group { Id = Guid.NewGuid(), Name = "Trip", InviteCode = "ABCDEFGH", CreatedByUserId = aliceUser.Id, CreatedAt = DateTime.UtcNow };
        db.Groups.Add(group);
        var alice = new Member { Id = Guid.NewGuid(), GroupId = group.Id, UserId = aliceUser.Id, DisplayName = "Alice", IsGuest = false, JoinedAt = DateTime.UtcNow };
        var bob = new Member { Id = Guid.NewGuid(), GroupId = group.Id, UserId = null, DisplayName = "Bob", IsGuest = true, JoinedAt = DateTime.UtcNow };
        db.Members.AddRange(alice, bob);
        await db.SaveChangesAsync();
        return new SeededGroup(group, aliceUser, alice, bob);
    }

    private static async Task<Member> AddCarolAsync(SplitwiseDbContext db, Group group)
    {
        var carol = new Member { Id = Guid.NewGuid(), GroupId = group.Id, UserId = null, DisplayName = "Carol", IsGuest = true, JoinedAt = DateTime.UtcNow };
        db.Members.Add(carol);
        await db.SaveChangesAsync();
        return carol;
    }

    private static AiChatRequest AnyRequest() => new() { Messages = [new AiChatMessageDto { Role = "user", Content = "split $90 for dinner" }] };

    private static LogExpenseToolResult Resolved(string description, decimal total, string paidBy, List<string> splitMembers, List<LogExpensePersonalItem>? personalItems = null)
        => new(description, total, paidBy, splitMembers, personalItems ?? [], false, null);

    private static LogExpenseToolResult NeedsClarificationResult(string question)
        => new("", 0, "", [], [], true, question);

    [Fact]
    public async Task ProcessMessageAsync_UnknownGroup_ReturnsGroupNotFound()
    {
        using var db = CreateDb();
        var sut = new AiChatService(db, new FakeAiExpenseParser(Resolved("x", 1, "x", [])));

        var result = await sut.ProcessMessageAsync(Guid.NewGuid(), Guid.NewGuid(), AnyRequest());

        Assert.False(result.Succeeded);
        Assert.Equal(AiChatError.GroupNotFound, result.Error);
    }

    [Fact]
    public async Task ProcessMessageAsync_RequesterNotAMember_ReturnsForbidden()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var sut = new AiChatService(db, new FakeAiExpenseParser(Resolved("x", 1, "x", [])));

        var result = await sut.ProcessMessageAsync(seed.Group.Id, Guid.NewGuid(), AnyRequest());

        Assert.False(result.Succeeded);
        Assert.Equal(AiChatError.Forbidden, result.Error);
    }

    [Fact]
    public async Task ProcessMessageAsync_AiRequestsClarification_PassesQuestionThrough()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var parser = new FakeAiExpenseParser(NeedsClarificationResult("How much did you spend?"));
        var sut = new AiChatService(db, parser);

        var result = await sut.ProcessMessageAsync(seed.Group.Id, seed.AliceUser.Id, AnyRequest());

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.NeedsClarification);
        Assert.Equal("How much did you spend?", result.Value.ClarificationQuestion);
        Assert.Null(result.Value.Suggestion);
    }

    [Fact]
    public async Task ProcessMessageAsync_AllSplitMembersNoPersonalItems_SplitsEvenlyAmongAll()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var toolResult = Resolved("Dinner", 90m, "alice", ["Alice", "bob"]); // case-insensitive names
        var sut = new AiChatService(db, new FakeAiExpenseParser(toolResult));

        var result = await sut.ProcessMessageAsync(seed.Group.Id, seed.AliceUser.Id, AnyRequest());

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.NeedsClarification);
        Assert.Equal(seed.Alice.Id, result.Value.Suggestion!.PaidByMemberId);
        Assert.Equal(2, result.Value.Suggestion.Shares.Count);
        Assert.Contains(result.Value.Suggestion.Shares, s => s.MemberId == seed.Bob.Id && s.Amount == 45m);
        Assert.Contains(result.Value.Suggestion.Shares, s => s.MemberId == seed.Alice.Id && s.Amount == 45m);
    }

    [Fact]
    public async Task ProcessMessageAsync_PersonalExpenseSingleSplitMember_ChargesThatPersonTheFullAmount()
    {
        // "I bought groceries just for myself" — a single split member gets the whole total.
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var toolResult = Resolved("Groceries", 20m, "Alice", ["Alice"]);
        var sut = new AiChatService(db, new FakeAiExpenseParser(toolResult));

        var result = await sut.ProcessMessageAsync(seed.Group.Id, seed.AliceUser.Id, AnyRequest());

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.NeedsClarification);
        var share = Assert.Single(result.Value.Suggestion!.Shares);
        Assert.Equal(seed.Alice.Id, share.MemberId);
        Assert.Equal(20m, share.Amount);
    }

    [Fact]
    public async Task ProcessMessageAsync_PaidByNameNotInGroup_ReturnsClarification()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var toolResult = Resolved("Dinner", 90m, "Zoe", ["Alice"]);
        var sut = new AiChatService(db, new FakeAiExpenseParser(toolResult));

        var result = await sut.ProcessMessageAsync(seed.Group.Id, seed.AliceUser.Id, AnyRequest());

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.NeedsClarification);
        Assert.Contains("Zoe", result.Value.ClarificationQuestion);
    }

    [Fact]
    public async Task ProcessMessageAsync_SplitMemberNameNotInGroup_ReturnsClarification()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var toolResult = Resolved("Dinner", 90m, "Alice", ["Zoe"]);
        var sut = new AiChatService(db, new FakeAiExpenseParser(toolResult));

        var result = await sut.ProcessMessageAsync(seed.Group.Id, seed.AliceUser.Id, AnyRequest());

        Assert.True(result.Value!.NeedsClarification);
        Assert.Contains("Zoe", result.Value.ClarificationQuestion);
    }

    [Fact]
    public async Task ProcessMessageAsync_PersonalItemMemberNameNotInGroup_ReturnsClarification()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var toolResult = Resolved("Dinner", 90m, "Alice", ["Alice", "Bob"], [new LogExpensePersonalItem("Zoe", 5m)]);
        var sut = new AiChatService(db, new FakeAiExpenseParser(toolResult));

        var result = await sut.ProcessMessageAsync(seed.Group.Id, seed.AliceUser.Id, AnyRequest());

        Assert.True(result.Value!.NeedsClarification);
        Assert.Contains("Zoe", result.Value.ClarificationQuestion);
    }

    [Fact]
    public async Task ProcessMessageAsync_ZeroTotalAmount_ReturnsClarification()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var toolResult = Resolved("Dinner", 0m, "Alice", ["Alice"]);
        var sut = new AiChatService(db, new FakeAiExpenseParser(toolResult));

        var result = await sut.ProcessMessageAsync(seed.Group.Id, seed.AliceUser.Id, AnyRequest());

        Assert.True(result.Value!.NeedsClarification);
    }

    [Fact]
    public async Task ProcessMessageAsync_PersonalItemPlusSplitRemainder_ComputesDivisionInCode()
    {
        // "$2 of the $20 was my own personal item, split the rest evenly between the other two"
        // — Alice still participates in splitting the remainder too, on top of her $2 extra.
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var carol = await AddCarolAsync(db, seed.Group);
        var toolResult = Resolved(
            "Groceries", 20m, "Alice",
            ["Alice", "Bob", "Carol"],
            [new LogExpensePersonalItem("Alice", 2m)]);
        var sut = new AiChatService(db, new FakeAiExpenseParser(toolResult));

        var result = await sut.ProcessMessageAsync(seed.Group.Id, seed.AliceUser.Id, AnyRequest());

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.NeedsClarification);
        var shares = result.Value.Suggestion!.Shares;
        Assert.Equal(3, shares.Count);
        Assert.Equal(20m, shares.Sum(s => s.Amount));
        // remainder = 20 - 2 = 18, split 3 ways = 6 each; Alice also gets +2 on top.
        Assert.Contains(shares, s => s.MemberId == seed.Alice.Id && s.Amount == 8m);
        Assert.Contains(shares, s => s.MemberId == seed.Bob.Id && s.Amount == 6m);
        Assert.Contains(shares, s => s.MemberId == carol.Id && s.Amount == 6m);
    }

    [Fact]
    public async Task ProcessMessageAsync_PersonalItemForExcludedSplitMember_ComputesDivisionAmongTheRest()
    {
        // Reproduces the exact bug scenario: $20 total, Alice's own $2.25 item, remainder
        // split between Bob and Carol only (Alice excluded from splitMembers this time).
        // 17.75 / 2 = 8.875, not a whole cent — must still land on exactly 20 total.
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var carol = await AddCarolAsync(db, seed.Group);
        var toolResult = Resolved(
            "Groceries", 20m, "Alice",
            ["Bob", "Carol"],
            [new LogExpensePersonalItem("Alice", 2.25m)]);
        var sut = new AiChatService(db, new FakeAiExpenseParser(toolResult));

        var result = await sut.ProcessMessageAsync(seed.Group.Id, seed.AliceUser.Id, AnyRequest());

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.NeedsClarification);
        var shares = result.Value.Suggestion!.Shares;
        Assert.Equal(20m, shares.Sum(s => s.Amount)); // exact — no floating/rounding drift
        Assert.Contains(shares, s => s.MemberId == seed.Alice.Id && s.Amount == 2.25m);
        var bobShare = shares.Single(s => s.MemberId == seed.Bob.Id).Amount;
        var carolShare = shares.Single(s => s.MemberId == carol.Id).Amount;
        Assert.Equal(17.75m, bobShare + carolShare);
        Assert.True(bobShare is 8.87m or 8.88m);
        Assert.True(carolShare is 8.87m or 8.88m);
    }

    [Fact]
    public async Task ProcessMessageAsync_PersonalItemsExceedTotal_ReturnsClarificationInsteadOfNegativeRemainder()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var toolResult = Resolved(
            "Groceries", 20m, "Alice",
            ["Bob"],
            [new LogExpensePersonalItem("Alice", 25m)]); // personal item alone already exceeds the total
        var sut = new AiChatService(db, new FakeAiExpenseParser(toolResult));

        var result = await sut.ProcessMessageAsync(seed.Group.Id, seed.AliceUser.Id, AnyRequest());

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.NeedsClarification);
    }

    [Fact]
    public async Task ProcessMessageAsync_NoSplitMembersPersonalItemsDoNotMatchTotal_ReturnsClarification()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var toolResult = Resolved("Dinner", 90m, "Alice", [], [new LogExpensePersonalItem("Alice", 30m), new LogExpensePersonalItem("Bob", 30m)]); // sums to 60, not 90
        var sut = new AiChatService(db, new FakeAiExpenseParser(toolResult));

        var result = await sut.ProcessMessageAsync(seed.Group.Id, seed.AliceUser.Id, AnyRequest());

        Assert.True(result.Value!.NeedsClarification);
    }
}
