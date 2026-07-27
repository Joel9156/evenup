namespace Splitwise.Api.Services;

// Raw shape of the AI's log_expense tool call — member names as free text, not yet matched
// to real Member ids. AiChatService resolves and validates this against the actual group.
//
// SplitMembers is everyone who evenly divides the shared portion of the expense (total minus
// PersonalItems). PersonalItems are additive extras on top of that even share — e.g. "I also
// grabbed a toothbrush for myself, that's $2.25" — the person still gets their even split of
// the rest too, unless they're separately left out of SplitMembers.
public record LogExpenseToolResult(
    string Description,
    decimal TotalAmount,
    string PaidBy,
    List<string> SplitMembers,
    List<LogExpensePersonalItem> PersonalItems,
    bool NeedsClarification,
    string? ClarificationQuestion);

public record LogExpensePersonalItem(string MemberName, decimal Amount);
