using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Splitwise.Api.Dtos.Auth;
using Splitwise.Api.Extensions;
using Splitwise.Api.Services;

namespace Splitwise.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await authService.RegisterAsync(request, ct);
        if (!result.Succeeded)
        {
            return Conflict(new { message = "An account with this email already exists." });
        }

        return Ok(result.Value);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, ct);
        if (!result.Succeeded)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken ct)
    {
        var me = await authService.GetMeAsync(User.GetUserId(), ct);
        return me is null ? NotFound() : Ok(me);
    }

    [Authorize]
    [HttpPut("me/account")]
    public async Task<ActionResult<UpdateAccountResponse>> UpdateAccount(UpdateAccountRequest request, CancellationToken ct)
    {
        var result = await authService.UpdateAccountAsync(User.GetUserId(), request, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
