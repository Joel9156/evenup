namespace EvenUp.Api.Services;

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
    string? ClarificationQuestion,
    // Set when the user is asking to correct/re-split an expense already listed in the
    // "expenses you can edit" prompt context, rather than logging a new one. Must exactly
    // match one of the ids given in that context — AiChatService re-validates it regardless.
    string? EditExpenseId = null);

public record LogExpensePersonalItem(string MemberName, decimal Amount);

// Prompt context for one expense the requesting member is allowed to edit (they created it).
// Given to the model so it can match a vague reference ("the cinema one") to a real expense
// and carry over whatever the user doesn't explicitly change, instead of re-asking for
// details it could already see.
public record EditableExpenseContext(Guid Id, string Description, decimal TotalAmount, string PaidByName, List<LogExpensePersonalItem> Shares);
