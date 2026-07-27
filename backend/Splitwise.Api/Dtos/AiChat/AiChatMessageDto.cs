using System.ComponentModel.DataAnnotations;

namespace Splitwise.Api.Dtos.AiChat;

public class AiChatMessageDto
{
    // "user" or "assistant" — mirrors the roles in the visible chat log so the frontend can
    // resend the whole conversation each turn (no server-side chat session state).
    [Required]
    public string Role { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;
}
