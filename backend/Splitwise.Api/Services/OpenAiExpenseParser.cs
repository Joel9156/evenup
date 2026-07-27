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
            "description": { "type": "string", "description": "지출 내용 (예: 저녁식사, 택시비)" },
            "totalAmount": { "type": "number" },
            "paidBy": { "type": "string", "description": "지출한 사람의 이름" },
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
            "clarificationQuestion": { "type": "string", "description": "정보가 부족할 때 되물을 질문" }
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
            Tools = { ChatTool.CreateFunctionTool(ToolName, "그룹 지출 항목을 구조화된 형태로 기록한다", BinaryData.FromString(ToolParametersJson)) },
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
        너는 그룹 지출 정산 앱의 AI 비서다.
        사용자의 자연어 입력을 분석해서 log_expense 도구를 호출해라.

        규칙:
        - 그룹 멤버 목록: {string.Join(", ", memberNames)}
        - 금액이나 인원이 불명확하면 needsClarification=true로 하고 무엇이 부족한지 명시해라.
        - 균등 분배가 기본이지만, 사용자가 "나만 뺴/더" 같은 조정을 언급하면 반영해라.
        - 화폐 단위는 별도 언급 없으면 그룹 기본 통화로 간주해라.
        """;

    private record LogExpenseArgs(string Description, decimal TotalAmount, string PaidBy, List<LogExpenseShareArgJson> Shares, bool NeedsClarification, string? ClarificationQuestion);

    private record LogExpenseShareArgJson(string MemberName, decimal Amount);
}
