using Splitwise.Api.Dtos.AiChat;

namespace Splitwise.Api.Services;

public interface IAiExpenseParser
{
    Task<LogExpenseToolResult> ParseAsync(IReadOnlyList<string> memberNames, IReadOnlyList<AiChatMessageDto> conversation, CancellationToken ct = default);
}
