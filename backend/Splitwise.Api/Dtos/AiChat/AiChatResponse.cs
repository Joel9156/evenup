namespace Splitwise.Api.Dtos.AiChat;

public class AiChatResponse
{
    public bool NeedsClarification { get; set; }

    // Set when NeedsClarification is true — shown in the chat UI, the user's reply becomes
    // the next turn in the conversation the frontend resends.
    public string? ClarificationQuestion { get; set; }

    // Set when NeedsClarification is false — shown as a confirm/edit card, never saved
    // automatically. A real expense is only created once the user confirms it via the
    // existing POST /api/groups/{groupId}/expenses endpoint.
    public ExpenseSuggestion? Suggestion { get; set; }

    // Members added to the group as part of processing this message (already saved — unlike
    // Suggestion, these are applied immediately, not staged behind a confirm step). Empty if
    // the message didn't ask to add anyone.
    public List<string> AddedMembers { get; set; } = [];
}
