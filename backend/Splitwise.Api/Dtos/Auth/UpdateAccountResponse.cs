namespace Splitwise.Api.Dtos.Auth;

public class UpdateAccountResponse
{
    public string BankName { get; set; } = string.Empty;

    // Masked (e.g. "****1234") — the full number is only decrypted when actually
    // needed, such as when generating a settlement message.
    public string MaskedAccountNumber { get; set; } = string.Empty;
}
