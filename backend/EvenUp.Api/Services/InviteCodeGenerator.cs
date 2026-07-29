using System.Security.Cryptography;

namespace EvenUp.Api.Services;

// Cryptographically random, unguessable — this code is the only thing standing between
// a shared link and "anyone can join this group." Alphabet excludes visually ambiguous
// characters (0/O, 1/I/L) since it's meant to be read off a chat message or typed by hand.
public class InviteCodeGenerator : IInviteCodeGenerator
{
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int Length = 8;

    public string Generate() => RandomNumberGenerator.GetString(Alphabet, Length);
}
