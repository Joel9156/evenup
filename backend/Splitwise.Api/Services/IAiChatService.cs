using Splitwise.Api.Dtos.AiChat;

namespace Splitwise.Api.Services;

public interface IAiChatService
{
    Task<AiChatResult<AiChatResponse>> ProcessMessageAsync(Guid groupId, Guid requestingUserId, AiChatRequest request, CancellationToken ct = default);
}
