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

        // personalItems are ADDITIVE — a member with one still gets their even share of the
        // shared portion too (splitMembers), on top of it. Nothing here does the actual
        // division; that's computed below, deterministically, rather than trusting the model
        // to divide decimals correctly.
        var personalItems = new List<(Member Member, decimal Amount)>();
        foreach (var item in toolResult.PersonalItems)
        {
            if (item.Amount <= 0)
            {
                continue; // not a real extra charge
            }

            if (!membersByName.TryGetValue(item.MemberName, out var member))
            {
                return AiChatResult<AiChatResponse>.Ok(NeedsClarification($"I couldn't find '{item.MemberName}' in this group. Could you give the exact name again?"));
            }

            personalItems.Add((member, item.Amount));
        }

        var splitMembers = new List<Member>();
        foreach (var name in toolResult.SplitMembers)
        {
            if (!membersByName.TryGetValue(name, out var member))
            {
                return AiChatResult<AiChatResponse>.Ok(NeedsClarification($"I couldn't find '{name}' in this group. Could you give the exact name again?"));
            }

            splitMembers.Add(member);
        }

        if (toolResult.TotalAmount <= 0 || (splitMembers.Count == 0 && personalItems.Count == 0))
        {
            return AiChatResult<AiChatResponse>.Ok(NeedsClarification("The amount or who's splitting it isn't clear. Could you say that again?"));
        }

        var personalSum = personalItems.Sum(p => p.Amount);
        var amounts = new Dictionary<Guid, decimal>();
        var membersById = new Dictionary<Guid, Member>();

        void AddAmount(Member member, decimal amount)
        {
            amounts[member.Id] = amounts.GetValueOrDefault(member.Id) + amount;
            membersById[member.Id] = member;
        }

        if (splitMembers.Count > 0)
        {
            var remainder = toolResult.TotalAmount - personalSum;
            if (remainder <= 0)
            {
                return AiChatResult<AiChatResponse>.Ok(NeedsClarification(
                    $"The personal amounts already add up to {personalSum:N2}, which leaves nothing to split among the rest. Could you double-check that?"));
            }

            // Round to the cent, then give any leftover cent(s) to the last person so the
            // shares always sum to exactly `remainder` — no floating drift to reconcile.
            var perPerson = Math.Round(remainder / splitMembers.Count, 2, MidpointRounding.AwayFromZero);
            for (var i = 0; i < splitMembers.Count; i++)
            {
                var isLast = i == splitMembers.Count - 1;
                var amount = isLast ? remainder - (perPerson * (splitMembers.Count - 1)) : perPerson;
                AddAmount(splitMembers[i], amount);
            }
        }
        else if (Math.Abs(personalSum - toolResult.TotalAmount) > AmountTolerance)
        {
            return AiChatResult<AiChatResponse>.Ok(NeedsClarification(
                $"The amounts add up to {personalSum:N2}, which doesn't match the total of {toolResult.TotalAmount:N2}. Could you double-check that?"));
        }

        foreach (var (member, amount) in personalItems)
        {
            AddAmount(member, amount);
        }

        var resolvedShares = amounts
            .Where(kv => kv.Value > 0)
            .Select(kv => (Member: membersById[kv.Key], Amount: kv.Value))
            .ToList();

        if (resolvedShares.Count == 0)
        {
            return AiChatResult<AiChatResponse>.Ok(NeedsClarification("The amount or who's splitting it isn't clear. Could you say that again?"));
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
