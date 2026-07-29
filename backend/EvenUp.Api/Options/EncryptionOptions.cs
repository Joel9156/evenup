namespace EvenUp.Api.Options;

public class EncryptionOptions
{
    public const string SectionName = "Encryption";

    // Base64-encoded AES-256 key (32 bytes) used to encrypt bank account numbers.
    // Provided via user-secrets locally / environment variable in deployment — never committed.
    public string AesKeyBase64 { get; set; } = string.Empty;
}
