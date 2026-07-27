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

    private static AiChatRequest AnyRequest() => new() { Messages = [new AiChatMessageDto { Role = "user", Content = "split $90 for dinner" }] };

    [Fact]
    public async Task ProcessMessageAsync_UnknownGroup_ReturnsGroupNotFound()
    {
        using var db = CreateDb();
        var sut = new AiChatService(db, new FakeAiExpenseParser(new LogExpenseToolResult("x", 1, "x", [], false, null)));

        var result = await sut.ProcessMessageAsync(Guid.NewGuid(), Guid.NewGuid(), AnyRequest());

        Assert.False(result.Succeeded);
        Assert.Equal(AiChatError.GroupNotFound, result.Error);
    }

    [Fact]
    public async Task ProcessMessageAsync_RequesterNotAMember_ReturnsForbidden()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var sut = new AiChatService(db, new FakeAiExpenseParser(new LogExpenseToolResult("x", 1, "x", [], false, null)));

        var result = await sut.ProcessMessageAsync(seed.Group.Id, Guid.NewGuid(), AnyRequest());

        Assert.False(result.Succeeded);
        Assert.Equal(AiChatError.Forbidden, result.Error);
    }

    [Fact]
    public async Task ProcessMessageAsync_AiRequestsClarification_PassesQuestionThrough()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var parser = new FakeAiExpenseParser(new LogExpenseToolResult("", 0, "", [], true, "How much did you spend?"));
        var sut = new AiChatService(db, parser);

        var result = await sut.ProcessMessageAsync(seed.Group.Id, seed.AliceUser.Id, AnyRequest());

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.NeedsClarification);
        Assert.Equal("How much did you spend?", result.Value.ClarificationQuestion);
        Assert.Null(result.Value.Suggestion);
    }

    [Fact]
    public async Task ProcessMessageAsync_ValidNamesMatchingMembers_ResolvesToSuggestion()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var toolResult = new LogExpenseToolResult(
            "Dinner", 90m, "alice", // lowercase — must still match "Alice" case-insensitively
            [new LogExpenseShareArg("Alice", 45m), new LogExpenseShareArg("bob", 45m)],
            false, null);
        var sut = new AiChatService(db, new FakeAiExpenseParser(toolResult));

        var result = await sut.ProcessMessageAsync(seed.Group.Id, seed.AliceUser.Id, AnyRequest());

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.NeedsClarification);
        Assert.Equal(seed.Alice.Id, result.Value.Suggestion!.PaidByMemberId);
        Assert.Equal(2, result.Value.Suggestion.Shares.Count);
        Assert.Contains(result.Value.Suggestion.Shares, s => s.MemberId == seed.Bob.Id && s.Amount == 45m);
    }

    [Fact]
    public async Task ProcessMessageAsync_AiListsNonParticipantsAtZero_DropsThemFromTheSuggestion()
    {
        // Reproduces a real bug: for "I bought groceries just for myself," the AI listed
        // every group member but gave the non-participants $0 instead of omitting them —
        // and CreateExpenseRequest rejects any share amount <= 0, so submitting the
        // suggestion as-is would fail outright.
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var toolResult = new LogExpenseToolResult(
            "Groceries", 20m, "Alice",
            [new LogExpenseShareArg("Alice", 20m), new LogExpenseShareArg("Bob", 0m)],
            false, null);
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
        var toolResult = new LogExpenseToolResult("Dinner", 90m, "Zoe", [new LogExpenseShareArg("Alice", 90m)], false, null);
        var sut = new AiChatService(db, new FakeAiExpenseParser(toolResult));

        var result = await sut.ProcessMessageAsync(seed.Group.Id, seed.AliceUser.Id, AnyRequest());

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.NeedsClarification);
        Assert.Contains("Zoe", result.Value.ClarificationQuestion);
    }

    [Fact]
    public async Task ProcessMessageAsync_ShareMemberNameNotInGroup_ReturnsClarification()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var toolResult = new LogExpenseToolResult("Dinner", 90m, "Alice", [new LogExpenseShareArg("Zoe", 90m)], false, null);
        var sut = new AiChatService(db, new FakeAiExpenseParser(toolResult));

        var result = await sut.ProcessMessageAsync(seed.Group.Id, seed.AliceUser.Id, AnyRequest());

        Assert.True(result.Value!.NeedsClarification);
        Assert.Contains("Zoe", result.Value.ClarificationQuestion);
    }

    [Fact]
    public async Task ProcessMessageAsync_SharesDoNotSumToTotal_ReturnsClarification()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var toolResult = new LogExpenseToolResult(
            "Dinner", 90m, "Alice",
            [new LogExpenseShareArg("Alice", 30m), new LogExpenseShareArg("Bob", 30m)], // sums to 60, not 90
            false, null);
        var sut = new AiChatService(db, new FakeAiExpenseParser(toolResult));

        var result = await sut.ProcessMessageAsync(seed.Group.Id, seed.AliceUser.Id, AnyRequest());

        Assert.True(result.Value!.NeedsClarification);
    }

    [Fact]
    public async Task ProcessMessageAsync_ZeroTotalAmount_ReturnsClarification()
    {
        using var db = CreateDb();
        var seed = await SeedGroupAsync(db);
        var toolResult = new LogExpenseToolResult("Dinner", 0m, "Alice", [new LogExpenseShareArg("Alice", 0m)], false, null);
        var sut = new AiChatService(db, new FakeAiExpenseParser(toolResult));

        var result = await sut.ProcessMessageAsync(seed.Group.Id, seed.AliceUser.Id, AnyRequest());

        Assert.True(result.Value!.NeedsClarification);
    }
}
