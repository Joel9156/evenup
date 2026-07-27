namespace Splitwise.Api.Dtos.AiChat;

// Fully resolved and ready to submit as-is to POST /api/groups/{groupId}/expenses — every
// name the AI produced has already been matched to a real member id by AiChatService.
public class ExpenseSuggestion
{
    public string Description { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public Guid PaidByMemberId { get; set; }
    public string PaidByDisplayName { get; set; } = string.Empty;
    public List<ExpenseShareSuggestion> Shares { get; set; } = [];
}
