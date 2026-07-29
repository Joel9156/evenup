using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using EvenUp.Api.Data;
using EvenUp.Api.Dtos.Auth;
using EvenUp.Api.Options;
using EvenUp.Api.Services;
using Xunit;

namespace EvenUp.Api.Tests.Services;

public class AuthServiceTests
{
    private static AuthService CreateService(EvenUpDbContext db)
    {
        var jwtOptions = Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            Key = "unit-test-signing-key-at-least-32-bytes-long",
            ExpiryMinutes = 60,
        });

        var encryptionOptions = Microsoft.Extensions.Options.Options.Create(new EncryptionOptions
        {
            // 32 zero bytes, base64-encoded — a validly-shaped AES-256 key for tests.
            AesKeyBase64 = Convert.ToBase64String(new byte[32]),
        });

        return new AuthService(
            db,
            new BcryptPasswordHasher(),
            new JwtTokenGenerator(jwtOptions),
            new AesAccountEncryptionService(encryptionOptions));
    }

    private static EvenUpDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<EvenUpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EvenUpDbContext(options);
    }

    [Fact]
    public async Task RegisterAsync_WithNewEmail_Succeeds()
    {
        using var db = CreateDb();
        var sut = CreateService(db);

        var result = await sut.RegisterAsync(new RegisterRequest
        {
            Email = "alice@example.com",
            Password = "password123",
            DisplayName = "Alice",
        });

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
        Assert.False(string.IsNullOrEmpty(result.Value!.Token));
        Assert.Equal("alice@example.com", result.Value.Email);
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_Fails()
    {
        using var db = CreateDb();
        var sut = CreateService(db);

        await sut.RegisterAsync(new RegisterRequest
        {
            Email = "alice@example.com",
            Password = "password123",
            DisplayName = "Alice",
        });

        var result = await sut.RegisterAsync(new RegisterRequest
        {
            Email = "ALICE@example.com", // same email, different casing
            Password = "anotherpassword",
            DisplayName = "Alice Again",
        });

        Assert.False(result.Succeeded);
        Assert.Equal(AuthError.EmailAlreadyExists, result.Error);
    }

    [Fact]
    public async Task LoginAsync_WithCorrectPassword_Succeeds()
    {
        using var db = CreateDb();
        var sut = CreateService(db);

        await sut.RegisterAsync(new RegisterRequest
        {
            Email = "bob@example.com",
            Password = "correct-password",
            DisplayName = "Bob",
        });

        var result = await sut.LoginAsync(new LoginRequest
        {
            Email = "bob@example.com",
            Password = "correct-password",
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_Fails()
    {
        using var db = CreateDb();
        var sut = CreateService(db);

        await sut.RegisterAsync(new RegisterRequest
        {
            Email = "bob@example.com",
            Password = "correct-password",
            DisplayName = "Bob",
        });

        var result = await sut.LoginAsync(new LoginRequest
        {
            Email = "bob@example.com",
            Password = "wrong-password",
        });

        Assert.False(result.Succeeded);
        Assert.Equal(AuthError.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownEmail_Fails()
    {
        using var db = CreateDb();
        var sut = CreateService(db);

        var result = await sut.LoginAsync(new LoginRequest
        {
            Email = "nobody@example.com",
            Password = "whatever",
        });

        Assert.False(result.Succeeded);
        Assert.Equal(AuthError.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task UpdateAccountAsync_EncryptsNumberAndReturnsMaskedValue()
    {
        using var db = CreateDb();
        var sut = CreateService(db);

        var registerResult = await sut.RegisterAsync(new RegisterRequest
        {
            Email = "carol@example.com",
            Password = "password123",
            DisplayName = "Carol",
        });

        var response = await sut.UpdateAccountAsync(registerResult.Value!.UserId, new UpdateAccountRequest
        {
            BankName = "Kiwibank",
            AccountNumber = "1234567890",
        });

        Assert.NotNull(response);
        Assert.Equal("Kiwibank", response!.BankName);
        Assert.Equal("******7890", response.MaskedAccountNumber);

        var storedUser = await db.Users.FindAsync(registerResult.Value.UserId);
        Assert.NotNull(storedUser!.AccountNumberEncrypted);
        Assert.NotEqual("1234567890", storedUser.AccountNumberEncrypted); // stored value must not be plaintext
    }
}
