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

        var myMember = group.Members.FirstOrDefault(m => m.UserId == requestingUserId);
        if (myMember is null)
        {
            return AiChatResult<AiChatResponse>.Fail(AiChatError.Forbidden);
        }

        // Only expenses this user actually created are offered as edit candidates — editing
        // anything else would fail ExpenseService's creator-only permission check anyway, so
        // there's no point letting the model suggest it.
        var membersById = group.Members.ToDictionary(m => m.Id, m => m);
        var editableExpenseEntities = await db.Expenses
            .Include(e => e.Shares)
            .Where(e => e.GroupId == groupId && e.CreatedByMemberId == myMember.Id)
            .OrderByDescending(e => e.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        var editableExpenses = editableExpenseEntities.Select(e => new EditableExpenseContext(
            e.Id,
            e.Description,
            e.TotalAmount,
            membersById.GetValueOrDefault(e.PaidByMemberId)?.DisplayName ?? "Unknown",
            e.Shares.Select(s => new LogExpensePersonalItem(
                membersById.GetValueOrDefault(s.MemberId)?.DisplayName ?? "Unknown",
                s.ShareAmount)).ToList())).ToList();

        var parseResult = await aiExpenseParser.ParseAsync(
            group.Members.Select(m => m.DisplayName).ToList(),
            editableExpenses,
            request.Messages,
            ct);

        // Adding a member is applied immediately rather than staged behind a confirm click —
        // it's exactly the same low-stakes, instantly-reversible action as the "Add someone by
        // name" button on the group page (which is also unconfirmed), unlike expense math,
        // which stays behind a confirm card because getting it wrong costs someone real money.
        // Doing this before resolving the expense fields also means a compound request like
        // "add Anthony and split the cinema bill three ways" can refer to Anthony by name in
        // the same turn — he already exists as a member by the time splitMembers is resolved.
        var addedMembers = new List<Member>();
        foreach (var rawName in parseResult.MembersToAdd)
        {
            var name = rawName.Trim();
            if (name.Length == 0)
            {
                continue;
            }

            var alreadyExists = group.Members.Any(m => string.Equals(m.DisplayName, name, StringComparison.OrdinalIgnoreCase))
                || addedMembers.Any(m => string.Equals(m.DisplayName, name, StringComparison.OrdinalIgnoreCase));
            if (alreadyExists)
            {
                continue;
            }

            var newMember = new Member
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                UserId = null,
                DisplayName = name,
                IsGuest = true,
                JoinedAt = DateTime.UtcNow,
            };

            // Not manually appended to group.Members — EF Core's relationship fixup already
            // does that as soon as this tracked entity's GroupId matches the tracked group,
            // and adding it again here would leave the collection with a duplicate entry.
            db.Members.Add(newMember);
            membersById[newMember.Id] = newMember;
            addedMembers.Add(newMember);
        }

        if (addedMembers.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        var addedMemberNames = addedMembers.Select(m => m.DisplayName).ToList();

        var toolResult = parseResult.Expense;
        if (toolResult is null)
        {
            // No expense-related tool call this turn — either the whole message was just about
            // adding members, or (defensively, since the model is required to call at least one
            // tool) neither happened, which shouldn't occur but shouldn't crash either.
            return AiChatResult<AiChatResponse>.Ok(addedMemberNames.Count > 0
                ? new AiChatResponse { NeedsClarification = false, AddedMembers = addedMemberNames }
                : NeedsClarification("Could you tell me more about what you'd like to do?", addedMemberNames));
        }

        if (toolResult.NeedsClarification)
        {
            return AiChatResult<AiChatResponse>.Ok(NeedsClarification(toolResult.ClarificationQuestion ?? "Could you give a few more details?", addedMemberNames));
        }

        var membersByName = group.Members.ToDictionary(m => m.DisplayName, m => m, StringComparer.OrdinalIgnoreCase);

        if (!membersByName.TryGetValue(toolResult.PaidBy, out var paidByMember))
        {
            return AiChatResult<AiChatResponse>.Ok(NeedsClarification($"I couldn't find '{toolResult.PaidBy}' in this group. Could you give the exact name again?", addedMemberNames));
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
                return AiChatResult<AiChatResponse>.Ok(NeedsClarification($"I couldn't find '{item.MemberName}' in this group. Could you give the exact name again?", addedMemberNames));
            }

            personalItems.Add((member, item.Amount));
        }

        var splitMembers = new List<Member>();
        foreach (var name in toolResult.SplitMembers)
        {
            if (!membersByName.TryGetValue(name, out var member))
            {
                return AiChatResult<AiChatResponse>.Ok(NeedsClarification($"I couldn't find '{name}' in this group. Could you give the exact name again?", addedMemberNames));
            }

            splitMembers.Add(member);
        }

        if (toolResult.TotalAmount <= 0 || (splitMembers.Count == 0 && personalItems.Count == 0))
        {
            return AiChatResult<AiChatResponse>.Ok(NeedsClarification("The amount or who's splitting it isn't clear. Could you say that again?", addedMemberNames));
        }

        var personalSum = personalItems.Sum(p => p.Amount);
        var amounts = new Dictionary<Guid, decimal>();

        void AddAmount(Member member, decimal amount)
        {
            amounts[member.Id] = amounts.GetValueOrDefault(member.Id) + amount;
        }

        if (splitMembers.Count > 0)
        {
            var remainder = toolResult.TotalAmount - personalSum;
            if (remainder <= 0)
            {
                return AiChatResult<AiChatResponse>.Ok(NeedsClarification(
                    $"The personal amounts already add up to {personalSum:N2}, which leaves nothing to split among the rest. Could you double-check that?", addedMemberNames));
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
                $"The amounts add up to {personalSum:N2}, which doesn't match the total of {toolResult.TotalAmount:N2}. Could you double-check that?", addedMemberNames));
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
            return AiChatResult<AiChatResponse>.Ok(NeedsClarification("The amount or who's splitting it isn't clear. Could you say that again?", addedMemberNames));
        }

        Guid? editingExpenseId = null;
        if (!string.IsNullOrWhiteSpace(toolResult.EditExpenseId))
        {
            // The model was only ever given ids from editableExpenseEntities, so a value that
            // doesn't parse or doesn't match one of them means it hallucinated — treat that as
            // "couldn't find it" rather than silently editing (or worse, guessing) the wrong thing.
            if (!Guid.TryParse(toolResult.EditExpenseId, out var parsedId) || editableExpenseEntities.All(e => e.Id != parsedId))
            {
                return AiChatResult<AiChatResponse>.Ok(NeedsClarification("I couldn't match that to one of your expenses. Could you describe it again?", addedMemberNames));
            }

            editingExpenseId = parsedId;
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
            EditingExpenseId = editingExpenseId,
        };

        return AiChatResult<AiChatResponse>.Ok(new AiChatResponse { NeedsClarification = false, Suggestion = suggestion, AddedMembers = addedMemberNames });
    }

    private static AiChatResponse NeedsClarification(string question, List<string> addedMembers) => new()
    {
        NeedsClarification = true,
        ClarificationQuestion = question,
        AddedMembers = addedMembers,
    };
}
