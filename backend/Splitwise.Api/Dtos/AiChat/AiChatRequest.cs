using System.ComponentModel.DataAnnotations;

namespace Splitwise.Api.Dtos.AiChat;

public class AiChatRequest
{
    [Required, MinLength(1)]
    public List<AiChatMessageDto> Messages { get; set; } = [];
}
