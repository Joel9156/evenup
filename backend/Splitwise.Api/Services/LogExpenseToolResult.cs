namespace Splitwise.Api.Services;

// Raw shape of the AI's log_expense tool call — member names as free text, not yet matched
// to real Member ids. AiChatService resolves and validates this against the actual group.
public record LogExpenseToolResult(
    string Description,
    decimal TotalAmount,
    string PaidBy,
    List<LogExpenseShareArg> Shares,
    bool NeedsClarification,
    string? ClarificationQuestion);

public record LogExpenseShareArg(string MemberName, decimal Amount);
