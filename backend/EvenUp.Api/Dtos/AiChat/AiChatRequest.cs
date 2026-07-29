using System.ComponentModel.DataAnnotations;

namespace EvenUp.Api.Dtos.AiChat;

public class AiChatRequest
{
    [Required, MinLength(1)]
    public List<AiChatMessageDto> Messages { get; set; } = [];
}
