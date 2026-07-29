using EvenUp.Api.Models;

namespace EvenUp.Api.Services;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
