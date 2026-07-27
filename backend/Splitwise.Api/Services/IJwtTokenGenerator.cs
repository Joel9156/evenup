using Splitwise.Api.Models;

namespace Splitwise.Api.Services;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
