using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using EvenUp.Api.Data;
using EvenUp.Api.Dtos.Balances;
using EvenUp.Api.Models;

namespace EvenUp.Api.Services;

public class BalanceService(EvenUpDbContext db, SettlementCalculator calculator) : IBalanceService
{
    public async Task<BalancesResponse?> GetBalancesAsync(Guid groupId, CancellationToken ct = default)
    {
        var computed = await ComputeAsync(groupId, ct);
        if (computed is null)
        {
            return null;
        }

        var (members, netBalances, transactions) = computed.Value;

        return new BalancesResponse
        {
            NetBalances = members.Select(m => new MemberBalanceResponse
            {
                MemberId = m.Id,
                DisplayName = m.DisplayName,
                NetBalance = netBalances.GetValueOrDefault(m.Id),
            }).ToList(),
            SuggestedTransactions = ToTransactionResponses(transactions, members),
        };
    }

    public async Task<SettleResponse?> SettleAsync(Guid groupId, CancellationToken ct = default)
    {
        var computed = await ComputeAsync(groupId, ct);
        if (computed is null)
        {
            return null;
        }

        var (members, _, transactions) = computed.Value;
        var transactionResponses = ToTransactionResponses(transactions, members);

        var settlement = new Settlement
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            GeneratedAt = DateTime.UtcNow,
            SnapshotJson = JsonSerializer.Serialize(transactionResponses),
        };

        db.Settlements.Add(settlement);
        await db.SaveChangesAsync(ct);

        return new SettleResponse
        {
            SettlementId = settlement.Id,
            GeneratedAt = settlement.GeneratedAt,
            Transactions = transactionResponses,
        };
    }

    private async Task<(List<Member> Members, Dictionary<Guid, decimal> NetBalances, List<SettlementTransaction> Transactions)?> ComputeAsync(Guid groupId, CancellationToken ct)
    {
        var group = await db.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null)
        {
            return null;
        }

        var expenses = await db.Expenses
            .Include(e => e.Shares)
            .Where(e => e.GroupId == groupId)
            .ToListAsync(ct);

        var netBalances = group.Members.ToDictionary(m => m.Id, _ => 0m);

        foreach (var expense in expenses)
        {
            netBalances[expense.PaidByMemberId] = netBalances.GetValueOrDefault(expense.PaidByMemberId) + expense.TotalAmount;

            foreach (var share in expense.Shares)
            {
                netBalances[share.MemberId] = netBalances.GetValueOrDefault(share.MemberId) - share.ShareAmount;
            }
        }

        var transactions = calculator.Calculate(netBalances);

        return (group.Members.ToList(), netBalances, transactions);
    }

    private static List<SettlementTransactionResponse> ToTransactionResponses(List<SettlementTransaction> transactions, List<Member> members)
    {
        var namesById = members.ToDictionary(m => m.Id, m => m.DisplayName);

        return transactions.Select(t => new SettlementTransactionResponse
        {
            FromMemberId = t.FromMemberId,
            FromDisplayName = namesById.GetValueOrDefault(t.FromMemberId, "Unknown"),
            ToMemberId = t.ToMemberId,
            ToDisplayName = namesById.GetValueOrDefault(t.ToMemberId, "Unknown"),
            Amount = t.Amount,
        }).ToList();
    }
}
