using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Splitwise.Api.Dtos.AiChat;
using Splitwise.Api.Options;

namespace Splitwise.Api.Services;

public class OpenAiExpenseParser : IAiExpenseParser
{
    private const string ToolName = "log_expense";

    // Matches the JSON schema in specs/splitwise-clone-spec.md section 7 exactly.
    private const string ToolParametersJson = """
        {
          "type": "object",
          "properties": {
            "description": { "type": "string", "description": "What the expense was for (e.g. dinner, taxi fare)" },
            "totalAmount": { "type": "number" },
            "paidBy": { "type": "string", "description": "Name of the person who paid" },
            "shares": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "memberName": { "type": "string" },
                  "amount": { "type": "number" }
                },
                "required": ["memberName", "amount"]
              }
            },
            "needsClarification": { "type": "boolean" },
            "clarificationQuestion": { "type": "string", "description": "The follow-up question to ask when information is missing" }
          },
          "required": ["description", "totalAmount", "paidBy", "shares", "needsClarification"]
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IOptions<OpenAiOptions> _options;

    // Built lazily, on first actual use, rather than in the constructor: ChatClient throws
    // immediately if the API key is empty, and ASP.NET Core builds this whole dependency
    // chain just to construct the controller — so an eager client would 500 every AI-chat
    // request (even ones that get rejected by an earlier check, like "not a group member")
    // whenever no key is configured yet, instead of only failing when parsing is attempted.
    private ChatClient? _chatClient;

    public OpenAiExpenseParser(IOptions<OpenAiOptions> options)
    {
        _options = options;
    }

    public async Task<LogExpenseToolResult> ParseAsync(IReadOnlyList<string> memberNames, IReadOnlyList<AiChatMessageDto> conversation, CancellationToken ct = default)
    {
        _chatClient ??= new ChatClient(_options.Value.Model, _options.Value.ApiKey);

        var messages = new List<ChatMessage> { new SystemChatMessage(BuildSystemPrompt(memberNames)) };
        foreach (var turn in conversation)
        {
            messages.Add(turn.Role == "assistant"
                ? new AssistantChatMessage(turn.Content)
                : new UserChatMessage(turn.Content));
        }

        var options = new ChatCompletionOptions
        {
            Tools = { ChatTool.CreateFunctionTool(ToolName, "Records a group expense entry in structured form", BinaryData.FromString(ToolParametersJson)) },
            ToolChoice = ChatToolChoice.CreateFunctionChoice(ToolName),
        };

        var completion = await _chatClient.CompleteChatAsync(messages, options, ct);
        var toolCall = completion.Value.ToolCalls.FirstOrDefault()
            ?? throw new InvalidOperationException("The AI did not call the expected log_expense tool.");

        var args = JsonSerializer.Deserialize<LogExpenseArgs>(toolCall.FunctionArguments, JsonOptions)
            ?? throw new InvalidOperationException("Could not parse the AI's log_expense arguments.");

        return new LogExpenseToolResult(
            args.Description,
            args.TotalAmount,
            args.PaidBy,
            args.Shares.Select(s => new LogExpenseShareArg(s.MemberName, s.Amount)).ToList(),
            args.NeedsClarification,
            args.ClarificationQuestion);
    }

    private static string BuildSystemPrompt(IReadOnlyList<string> memberNames) => $"""
        You are the AI assistant for a group expense-splitting app.
        Analyze the user's natural-language input and call the log_expense tool.

        Rules:
        - Group members: {string.Join(", ", memberNames)}
        - If the amount or who paid is unclear, set needsClarification=true and state exactly what's missing.
        - If the message doesn't make clear whether this should be split among the group or is a
          personal expense charged to one person, don't guess either way — set
          needsClarification=true and ask something like "Should I split this evenly among
          everyone, or was this just for you?"
        - Only skip that question when the message already makes the split obvious — either by
          naming who's involved/excluded (e.g. "split between me and Bob", "not for Carol"), or
          by clearly describing something personal (e.g. "just for myself, don't split it").
        - If no currency is mentioned, assume the group's default currency.
        """;

    private record LogExpenseArgs(string Description, decimal TotalAmount, string PaidBy, List<LogExpenseShareArgJson> Shares, bool NeedsClarification, string? ClarificationQuestion);

    private record LogExpenseShareArgJson(string MemberName, decimal Amount);
}
