using Splitwise.Api.Dtos.Auth;

namespace Splitwise.Api.Services;

public interface IAuthService
{
    Task<AuthResult<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<MeResponse?> GetMeAsync(Guid userId, CancellationToken ct = default);
    Task<UpdateAccountResponse?> UpdateAccountAsync(Guid userId, UpdateAccountRequest request, CancellationToken ct = default);
}
