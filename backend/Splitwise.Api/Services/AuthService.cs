using Microsoft.EntityFrameworkCore;
using Splitwise.Api.Data;
using Splitwise.Api.Dtos.Auth;
using Splitwise.Api.Models;

namespace Splitwise.Api.Services;

public class AuthService(
    SplitwiseDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IAccountEncryptionService accountEncryption) : IAuthService
{
    public async Task<AuthResult<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailTaken = await db.Users.AnyAsync(u => u.Email == normalizedEmail, ct);
        if (emailTaken)
        {
            return AuthResult<AuthResponse>.Fail(AuthError.EmailAlreadyExists);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = passwordHasher.Hash(request.Password),
            DisplayName = request.DisplayName.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return AuthResult<AuthResponse>.Ok(ToAuthResponse(user));
    }

    public async Task<AuthResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return AuthResult<AuthResponse>.Fail(AuthError.InvalidCredentials);
        }

        return AuthResult<AuthResponse>.Ok(ToAuthResponse(user));
    }

    public async Task<MeResponse?> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.FindAsync([userId], ct);
        if (user is null)
        {
            return null;
        }

        return new MeResponse
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            BankName = user.BankName,
            HasAccountNumber = !string.IsNullOrEmpty(user.AccountNumberEncrypted),
            CreatedAt = user.CreatedAt,
        };
    }

    public async Task<UpdateAccountResponse?> UpdateAccountAsync(Guid userId, UpdateAccountRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.FindAsync([userId], ct);
        if (user is null)
        {
            return null;
        }

        user.BankName = request.BankName.Trim();
        user.AccountNumberEncrypted = accountEncryption.Encrypt(request.AccountNumber.Trim());
        await db.SaveChangesAsync(ct);

        return new UpdateAccountResponse
        {
            BankName = user.BankName,
            MaskedAccountNumber = MaskAccountNumber(request.AccountNumber.Trim()),
        };
    }

    private static string MaskAccountNumber(string accountNumber)
    {
        const int visibleDigits = 4;
        if (accountNumber.Length <= visibleDigits)
        {
            return new string('*', accountNumber.Length);
        }

        var maskedLength = accountNumber.Length - visibleDigits;
        return new string('*', maskedLength) + accountNumber[^visibleDigits..];
    }

    private AuthResponse ToAuthResponse(User user) => new()
    {
        Token = jwtTokenGenerator.GenerateToken(user),
        UserId = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
    };
}
