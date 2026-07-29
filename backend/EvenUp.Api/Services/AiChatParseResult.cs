namespace EvenUp.Api.Services;

// A single AI turn can request adding members and/or logging/editing an expense — e.g. "add
// Anthony and split the cinema cost between us" needs both, in that order (AiChatService adds
// MembersToAdd first so Expense's member names can resolve against the complete list).
// Expense is null when the turn was only about adding members (or, defensively, when the model
// didn't call either tool).
public record AiChatParseResult(List<string> MembersToAdd, LogExpenseToolResult? Expense);
