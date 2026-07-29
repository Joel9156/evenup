namespace EvenUp.Api.Dtos.Settlements;

public class SettlementMessageResponse
{
    public Guid FromMemberId { get; set; }
    public string FromDisplayName { get; set; } = string.Empty;
    public Guid ToMemberId { get; set; }
    public string ToDisplayName { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    // False when the recipient has no account on file and no override was supplied —
    // the message still generates, just without a "where to send it" line.
    public bool AccountInfoProvided { get; set; }

    public string MessageText { get; set; } = string.Empty;
    public string MailtoLink { get; set; } = string.Empty;
    public string WhatsAppLink { get; set; } = string.Empty;
}
