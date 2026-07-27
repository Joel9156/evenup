namespace Splitwise.Api.Services;

// Two-way (reversible) encryption — unlike password hashing, an account number must be
// decrypted again later to show it back to the person settling up.
public interface IAccountEncryptionService
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}
