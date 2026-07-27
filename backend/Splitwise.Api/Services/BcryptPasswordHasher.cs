namespace Splitwise.Api.Services;

// One-way hash — used for passwords, which only ever need to be verified, never recovered.
public class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
