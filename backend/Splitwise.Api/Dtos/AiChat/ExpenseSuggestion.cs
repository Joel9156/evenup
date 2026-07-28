namespace Splitwise.Api.Dtos.AiChat;

// Fully resolved and ready to submit as-is — every name the AI produced has already been
// matched to a real member id by AiChatService. Submit to POST /api/groups/{groupId}/expenses
// for a new expense, or PUT /api/expenses/{EditingExpenseId} when that's set (the user asked
// to correct/re-split something already logged, not create something new).
public class ExpenseSuggestion
{
    public string Description { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public Guid PaidByMemberId { get; set; }
    public string PaidByDisplayName { get; set; } = string.Empty;
    public List<ExpenseShareSuggestion> Shares { get; set; } = [];
    public Guid? EditingExpenseId { get; set; }
}
