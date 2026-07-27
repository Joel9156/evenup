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
            return AiChatResult<AiChatResponse>.Ok(NeedsClarification(toolResult.ClarificationQuestion ?? "조금 더 자세히 말씀해주시겠어요?"));
        }

        var membersByName = group.Members.ToDictionary(m => m.DisplayName, m => m, StringComparer.OrdinalIgnoreCase);

        if (!membersByName.TryGetValue(toolResult.PaidBy, out var paidByMember))
        {
            return AiChatResult<AiChatResponse>.Ok(NeedsClarification($"'{toolResult.PaidBy}'님을 그룹 멤버에서 찾을 수 없어요. 정확한 이름으로 다시 말씀해주시겠어요?"));
        }

        var resolvedShares = new List<(Member Member, decimal Amount)>();
        foreach (var share in toolResult.Shares)
        {
            if (!membersByName.TryGetValue(share.MemberName, out var member))
            {
                return AiChatResult<AiChatResponse>.Ok(NeedsClarification($"'{share.MemberName}'님을 그룹 멤버에서 찾을 수 없어요. 정확한 이름으로 다시 말씀해주시겠어요?"));
            }

            resolvedShares.Add((member, share.Amount));
        }

        if (toolResult.TotalAmount <= 0 || resolvedShares.Count == 0)
        {
            return AiChatResult<AiChatResponse>.Ok(NeedsClarification("금액이나 나눌 인원이 명확하지 않아요. 다시 한번 말씀해주시겠어요?"));
        }

        var shareSum = resolvedShares.Sum(s => s.Amount);
        if (Math.Abs(shareSum - toolResult.TotalAmount) > AmountTolerance)
        {
            return AiChatResult<AiChatResponse>.Ok(NeedsClarification($"나눈 금액의 합({shareSum:N0})이 총액({toolResult.TotalAmount:N0})과 맞지 않아요. 다시 한번 확인해주시겠어요?"));
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
