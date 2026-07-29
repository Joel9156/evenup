using EvenUp.Api.Dtos.AiChat;

namespace EvenUp.Api.Services;

public interface IAiChatService
{
    Task<AiChatResult<AiChatResponse>> ProcessMessageAsync(Guid groupId, Guid requestingUserId, AiChatRequest request, CancellationToken ct = default);
}
