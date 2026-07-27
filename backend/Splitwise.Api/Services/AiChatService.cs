using Microsoft.EntityFrameworkCore;
using Splitwise.Api.Data;
using Splitwise.Api.Dtos.AiChat;
using Splitwise.Api.Models;

namespace Splitwise.Api.Services;

public class AiChatService(SplitwiseDbContext db, IAiExpenseParser aiExpenseParser) : IAiChatService
{
    private const decimal AmountTolerance = 0.01m;

    public async Task<AiChatResult<AiChatResponse>> ProcessMessageAsync(Guid groupId, Guid requestingUserId, AiChatRequest request, CancellationToken ct = default)
    {
        var group = await db.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null)
        {
            return AiChatResult<AiChatResponse>.Fail(AiChatError.GroupNotFound);
        }

        if (!group.Members.Any(m => m.UserId == requestingUserId))
        {
            return AiChatResult<AiChatResponse>.Fail(AiChatError.Forbidden);
        }

        var toolResult = await aiExpenseParser.ParseAsync(
            group.Members.Select(m => m.DisplayName).ToList(),
            request.Messages,
            ct);

        if (toolResult.NeedsClarification)
        {
            return AiChatResult<AiChatResponse>.Ok(NeedsClarification(toolResult.ClarificationQuestion ?? "Could you give a few more details?"));
        }

        var membersByName = group.Members.ToDictionary(m => m.DisplayName, m => m, StringComparer.OrdinalIgnoreCase);

        if (!membersByName.TryGetValue(toolResult.PaidBy, out var paidByMember))
        {
            return AiChatResult<AiChatResponse>.Ok(NeedsClarification($"I couldn't find '{toolResult.PaidBy}' in this group. Could you give the exact name again?"));
        }

        var resolvedShares = new List<(Member Member, decimal Amount)>();
        foreach (var share in toolResult.Shares)
        {
            // The AI sometimes lists every group member with $0 for the ones who aren't
            // actually part of the split (e.g. "I bought this just for myself"), rather than
            // omitting them. A $0 line isn't a real share — CreateExpenseRequest requires
            // every share to be > 0 — so drop it here rather than pass it through and have
            // expense creation reject the whole thing.
            if (share.Amount <= 0)
            {
                continue;
            }

            if (!membersByName.TryGetValue(share.MemberName, out var member))
            {
                return AiChatResult<AiChatResponse>.Ok(NeedsClarification($"I couldn't find '{share.MemberName}' in this group. Could you give the exact name again?"));
            }

            resolvedShares.Add((member, share.Amount));
        }

        if (toolResult.TotalAmount <= 0 || resolvedShares.Count == 0)
        {
            return AiChatResult<AiChatResponse>.Ok(NeedsClarification("The amount or who's splitting it isn't clear. Could you say that again?"));
        }

        var shareSum = resolvedShares.Sum(s => s.Amount);
        if (Math.Abs(shareSum - toolResult.TotalAmount) > AmountTolerance)
        {
            return AiChatResult<AiChatResponse>.Ok(NeedsClarification($"The shares add up to {shareSum:N0}, which doesn't match the total of {toolResult.TotalAmount:N0}. Could you double-check that?"));
        }

        var suggestion = new ExpenseSuggestion
        {
            Description = toolResult.Description,
            TotalAmount = toolResult.TotalAmount,
            PaidByMemberId = paidByMember.Id,
            PaidByDisplayName = paidByMember.DisplayName,
            Shares = resolvedShares.Select(s => new ExpenseShareSuggestion
            {
                MemberId = s.Member.Id,
                DisplayName = s.Member.DisplayName,
                Amount = s.Amount,
            }).ToList(),
        };

        return AiChatResult<AiChatResponse>.Ok(new AiChatResponse { NeedsClarification = false, Suggestion = suggestion });
    }

    private static AiChatResponse NeedsClarification(string question) => new()
    {
        NeedsClarification = true,
        ClarificationQuestion = question,
    };
}
