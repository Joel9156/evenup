using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Splitwise.Api.Options;

namespace Splitwise.Api.Services;

// AES-256-GCM: authenticated encryption, so tampering with stored ciphertext is detected
// on decrypt rather than silently producing garbage. Output layout: nonce | ciphertext | tag.
public class AesAccountEncryptionService : IAccountEncryptionService
{
    private readonly byte[] _key;

    public AesAccountEncryptionService(IOptions<EncryptionOptions> options)
    {
        _key = Convert.FromBase64String(options.Value.AesKeyBase64);
    }

    public string Encrypt(string plaintext)
    {
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(nonce);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];

        using var aesGcm = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var result = new byte[nonce.Length + ciphertext.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(ciphertext, 0, result, nonce.Length, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length + ciphertext.Length, tag.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string ciphertext)
    {
        var data = Convert.FromBase64String(ciphertext);

        var nonceSize = AesGcm.NonceByteSizes.MaxSize;
        var tagSize = AesGcm.TagByteSizes.MaxSize;
        var cipherSize = data.Length - nonceSize - tagSize;

        var nonce = data.AsSpan(0, nonceSize);
        var cipherBytes = data.AsSpan(nonceSize, cipherSize);
        var tag = data.AsSpan(nonceSize + cipherSize, tagSize);

        var plaintextBytes = new byte[cipherSize];
        using var aesGcm = new AesGcm(_key, tagSize);
        aesGcm.Decrypt(nonce, cipherBytes, tag, plaintextBytes);

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
