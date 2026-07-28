using Splitwise.Api.Dtos.AiChat;

namespace Splitwise.Api.Services;

public interface IAiExpenseParser
{
    Task<AiChatParseResult> ParseAsync(
        IReadOnlyList<string> memberNames,
        IReadOnlyList<EditableExpenseContext> editableExpenses,
        IReadOnlyList<AiChatMessageDto> conversation,
        CancellationToken ct = default);
}
