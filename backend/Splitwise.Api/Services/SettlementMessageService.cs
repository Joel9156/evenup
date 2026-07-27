using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Splitwise.Api.Data;
using Splitwise.Api.Dtos.Balances;
using Splitwise.Api.Dtos.Settlements;
using Splitwise.Api.Options;

namespace Splitwise.Api.Services;

public class SettlementMessageService(
    SplitwiseDbContext db,
    IAccountEncryptionService accountEncryption,
    IOptions<FrontendOptions> frontendOptions) : ISettlementMessageService
{
    private record AccountInfo(string BankName, string AccountNumber);

    public async Task<List<SettlementMessageResponse>?> GenerateMessagesAsync(Guid settlementId, GenerateSettlementMessagesRequest request, CancellationToken ct = default)
    {
        var settlement = await db.Settlements
            .Include(s => s.Group)
            .FirstOrDefaultAsync(s => s.Id == settlementId, ct);

        if (settlement is null)
        {
            return null;
        }

        var transactions = JsonSerializer.Deserialize<List<SettlementTransactionResponse>>(settlement.SnapshotJson) ?? [];

        var members = await db.Members
            .Include(m => m.User)
            .Where(m => m.GroupId == settlement.GroupId)
            .ToListAsync(ct);
        var membersById = members.ToDictionary(m => m.Id);

        var overridesByMember = request.AccountOverrides.ToDictionary(o => o.MemberId, o => new AccountInfo(o.BankName, o.AccountNumber));

        var shareLink = $"{frontendOptions.Value.BaseUrl.TrimEnd('/')}/groups/{settlement.GroupId}/settle?settlementId={settlement.Id}";

        return transactions.Select(t => BuildMessage(t, settlement.Group.Name, membersById, overridesByMember, shareLink)).ToList();
    }

    private SettlementMessageResponse BuildMessage(
        SettlementTransactionResponse transaction,
        string groupName,
        Dictionary<Guid, Models.Member> membersById,
        Dictionary<Guid, AccountInfo> overrides,
        string shareLink)
    {
        var accountInfo = ResolveAccountInfo(transaction.ToMemberId, membersById, overrides);

        // TODO: currency is hardcoded to USD for now — plan is to make it a per-group setting later.
        var accountLine = accountInfo is null
            ? "Account: no account on file - please check with the recipient directly"
            : $"Account: {accountInfo.BankName} {accountInfo.AccountNumber} ({transaction.ToDisplayName})";

        var messageText = $"""
            [{groupName}] Settlement summary

            {transaction.ToDisplayName}, you're owed ${transaction.Amount:N2} from {transaction.FromDisplayName}.
            ({transaction.FromDisplayName}, please send ${transaction.Amount:N2} to {transaction.ToDisplayName})

            {accountLine}

            View the full settlement: {shareLink}
            """;

        return new SettlementMessageResponse
        {
            FromMemberId = transaction.FromMemberId,
            FromDisplayName = transaction.FromDisplayName,
            ToMemberId = transaction.ToMemberId,
            ToDisplayName = transaction.ToDisplayName,
            Amount = transaction.Amount,
            AccountInfoProvided = accountInfo is not null,
            MessageText = messageText,
            MailtoLink = $"mailto:?subject={Uri.EscapeDataString($"[{groupName}] Settlement summary")}&body={Uri.EscapeDataString(messageText)}",
            WhatsAppLink = $"https://wa.me/?text={Uri.EscapeDataString(messageText)}",
        };
    }

    private AccountInfo? ResolveAccountInfo(Guid creditorMemberId, Dictionary<Guid, Models.Member> membersById, Dictionary<Guid, AccountInfo> overrides)
    {
        if (membersById.TryGetValue(creditorMemberId, out var member) &&
            member.User is { AccountNumberEncrypted: not null, BankName: not null } user)
        {
            return new AccountInfo(user.BankName, accountEncryption.Decrypt(user.AccountNumberEncrypted));
        }

        return overrides.GetValueOrDefault(creditorMemberId);
    }
}
