namespace Splitwise.Api.Dtos.AiChat;

public class ExpenseShareSuggestion
{
    public Guid MemberId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
