using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Splitwise.Api.Data;
using Splitwise.Api.Dtos.Balances;
using Splitwise.Api.Dtos.Settlements;
using Splitwise.Api.Models;
using Splitwise.Api.Options;
using Splitwise.Api.Services;
using Xunit;

namespace Splitwise.Api.Tests.Services;

public class SettlementMessageServiceTests
{
    private static SplitwiseDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SplitwiseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SplitwiseDbContext(options);
    }

    private static AesAccountEncryptionService CreateEncryption()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new EncryptionOptions
        {
            AesKeyBase64 = Convert.ToBase64String(new byte[32]),
        });
        return new AesAccountEncryptionService(options);
    }

    private static SettlementMessageService CreateService(SplitwiseDbContext db, AesAccountEncryptionService encryption)
    {
        var frontendOptions = Microsoft.Extensions.Options.Options.Create(new FrontendOptions { BaseUrl = "http://localhost:5173" });
        return new SettlementMessageService(db, encryption, frontendOptions);
    }

    private record SeededScenario(Group Group, Member Alice, Member Bob, Settlement Settlement);

    // Alice (sign-in, has a bank account on file) is owed $40 by Bob (guest, no account on file).
    private static async Task<SeededScenario> SeedScenarioAsync(SplitwiseDbContext db, AesAccountEncryptionService encryption)
    {
        var aliceUser = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@example.com",
            PasswordHash = "x",
            DisplayName = "Alice",
            BankName = "Kiwibank",
            AccountNumberEncrypted = encryption.Encrypt("1234567890"),
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(aliceUser);

        var group = new Group { Id = Guid.NewGuid(), Name = "Ski Trip", InviteCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), CreatedByUserId = aliceUser.Id, CreatedAt = DateTime.UtcNow };
        db.Groups.Add(group);

        var alice = new Member { Id = Guid.NewGuid(), GroupId = group.Id, UserId = aliceUser.Id, DisplayName = "Alice", IsGuest = false, JoinedAt = DateTime.UtcNow };
        var bob = new Member { Id = Guid.NewGuid(), GroupId = group.Id, UserId = null, DisplayName = "Bob", IsGuest = true, JoinedAt = DateTime.UtcNow };
        db.Members.AddRange(alice, bob);

        var snapshot = new List<SettlementTransactionResponse>
        {
            new() { FromMemberId = bob.Id, FromDisplayName = "Bob", ToMemberId = alice.Id, ToDisplayName = "Alice", Amount = 40m },
        };

        var settlement = new Settlement
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            GeneratedAt = DateTime.UtcNow,
            SnapshotJson = JsonSerializer.Serialize(snapshot),
        };
        db.Settlements.Add(settlement);

        await db.SaveChangesAsync();

        return new SeededScenario(group, alice, bob, settlement);
    }

    [Fact]
    public async Task GenerateMessagesAsync_UnknownSettlement_ReturnsNull()
    {
        using var db = CreateDb();
        var sut = CreateService(db, CreateEncryption());

        var result = await sut.GenerateMessagesAsync(Guid.NewGuid(), new GenerateSettlementMessagesRequest());

        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateMessagesAsync_CreditorWithAccountOnFile_DecryptsAndIncludesAccountLine()
    {
        using var db = CreateDb();
        var encryption = CreateEncryption();
        var seed = await SeedScenarioAsync(db, encryption);
        var sut = CreateService(db, encryption);

        var result = await sut.GenerateMessagesAsync(seed.Settlement.Id, new GenerateSettlementMessagesRequest());

        var message = Assert.Single(result!);
        Assert.True(message.AccountInfoProvided);
        Assert.Contains("Kiwibank", message.MessageText);
        Assert.Contains("1234567890", message.MessageText); // decrypted, not the ciphertext
    }

    [Fact]
    public async Task GenerateMessagesAsync_CreditorIsGuestWithoutOverride_AccountInfoNotProvided()
    {
        using var db = CreateDb();
        var encryption = CreateEncryption();

        // Flip the scenario: make Bob (guest) the one who is owed money, so there's no
        // on-file account and no override supplied.
        var aliceUser = new User { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid()}@example.com", PasswordHash = "x", DisplayName = "Alice", CreatedAt = DateTime.UtcNow };
        db.Users.Add(aliceUser);
        var group = new Group { Id = Guid.NewGuid(), Name = "Trip", InviteCode = "ABCDEFGH", CreatedByUserId = aliceUser.Id, CreatedAt = DateTime.UtcNow };
        db.Groups.Add(group);
        var alice = new Member { Id = Guid.NewGuid(), GroupId = group.Id, UserId = aliceUser.Id, DisplayName = "Alice", IsGuest = false, JoinedAt = DateTime.UtcNow };
        var bob = new Member { Id = Guid.NewGuid(), GroupId = group.Id, UserId = null, DisplayName = "Bob", IsGuest = true, JoinedAt = DateTime.UtcNow };
        db.Members.AddRange(alice, bob);

        var snapshot = new List<SettlementTransactionResponse>
        {
            new() { FromMemberId = alice.Id, FromDisplayName = "Alice", ToMemberId = bob.Id, ToDisplayName = "Bob", Amount = 15m },
        };
        var settlement = new Settlement { Id = Guid.NewGuid(), GroupId = group.Id, GeneratedAt = DateTime.UtcNow, SnapshotJson = JsonSerializer.Serialize(snapshot) };
        db.Settlements.Add(settlement);
        await db.SaveChangesAsync();

        var sut = CreateService(db, encryption);

        var result = await sut.GenerateMessagesAsync(settlement.Id, new GenerateSettlementMessagesRequest());

        var message = Assert.Single(result!);
        Assert.False(message.AccountInfoProvided);
        Assert.Contains("등록된 계좌 정보 없음", message.MessageText);
    }

    [Fact]
    public async Task GenerateMessagesAsync_OverrideSuppliedForGuestCreditor_IsUsed()
    {
        using var db = CreateDb();
        var encryption = CreateEncryption();

        var aliceUser = new User { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid()}@example.com", PasswordHash = "x", DisplayName = "Alice", CreatedAt = DateTime.UtcNow };
        db.Users.Add(aliceUser);
        var group = new Group { Id = Guid.NewGuid(), Name = "Trip", InviteCode = "ZYXWVUTS", CreatedByUserId = aliceUser.Id, CreatedAt = DateTime.UtcNow };
        db.Groups.Add(group);
        var alice = new Member { Id = Guid.NewGuid(), GroupId = group.Id, UserId = aliceUser.Id, DisplayName = "Alice", IsGuest = false, JoinedAt = DateTime.UtcNow };
        var bob = new Member { Id = Guid.NewGuid(), GroupId = group.Id, UserId = null, DisplayName = "Bob", IsGuest = true, JoinedAt = DateTime.UtcNow };
        db.Members.AddRange(alice, bob);

        var snapshot = new List<SettlementTransactionResponse>
        {
            new() { FromMemberId = alice.Id, FromDisplayName = "Alice", ToMemberId = bob.Id, ToDisplayName = "Bob", Amount = 15m },
        };
        var settlement = new Settlement { Id = Guid.NewGuid(), GroupId = group.Id, GeneratedAt = DateTime.UtcNow, SnapshotJson = JsonSerializer.Serialize(snapshot) };
        db.Settlements.Add(settlement);
        await db.SaveChangesAsync();

        var sut = CreateService(db, encryption);

        var request = new GenerateSettlementMessagesRequest
        {
            AccountOverrides = [new AccountOverride { MemberId = bob.Id, BankName = "ANZ", AccountNumber = "99998888" }],
        };
        var result = await sut.GenerateMessagesAsync(settlement.Id, request);

        var message = Assert.Single(result!);
        Assert.True(message.AccountInfoProvided);
        Assert.Contains("ANZ", message.MessageText);
        Assert.Contains("99998888", message.MessageText);
    }

    [Fact]
    public async Task GenerateMessagesAsync_ShareLinks_AreUrlEncodedAndPointToCorrectHosts()
    {
        using var db = CreateDb();
        var encryption = CreateEncryption();
        var seed = await SeedScenarioAsync(db, encryption);
        var sut = CreateService(db, encryption);

        var result = await sut.GenerateMessagesAsync(seed.Settlement.Id, new GenerateSettlementMessagesRequest());

        var message = Assert.Single(result!);
        Assert.StartsWith("mailto:?subject=", message.MailtoLink);
        Assert.StartsWith("https://wa.me/?text=", message.WhatsAppLink);
        Assert.DoesNotContain(" ", message.WhatsAppLink); // spaces must be percent-encoded, not literal
    }
}
