using EvenUp.Api.Dtos.AiChat;

namespace EvenUp.Api.Services;

public interface IAiExpenseParser
{
    Task<AiChatParseResult> ParseAsync(
        IReadOnlyList<string> memberNames,
        IReadOnlyList<EditableExpenseContext> editableExpenses,
        IReadOnlyList<AiChatMessageDto> conversation,
        CancellationToken ct = default);
}
