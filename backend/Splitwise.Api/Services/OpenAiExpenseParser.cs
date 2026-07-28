using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Splitwise.Api.Dtos.AiChat;
using Splitwise.Api.Options;

namespace Splitwise.Api.Services;

public class OpenAiExpenseParser : IAiExpenseParser
{
    private const string LogExpenseToolName = "log_expense";
    private const string AddMemberToolName = "add_member";

    // Deliberately asks the model for structure (who's in the even split, who has an extra
    // personal amount) rather than final dollar figures — LLMs are reliable at extracting that
    // from natural language, not at exact multi-step division/rounding, so AiChatService does
    // that arithmetic itself instead of trusting whatever numbers come back from the prompt.
    private const string LogExpenseParametersJson = """
        {
          "type": "object",
          "properties": {
            "description": { "type": "string", "description": "What the expense was for (e.g. dinner, taxi fare)" },
            "totalAmount": { "type": "number" },
            "paidBy": { "type": "string", "description": "Name of the person who paid" },
            "splitMembers": {
              "type": "array",
              "description": "Everyone who evenly splits the shared portion of this expense (the total minus any personalItems amounts below). Do not include anyone the user excluded entirely.",
              "items": { "type": "string" }
            },
            "personalItems": {
              "type": "array",
              "description": "Extra individual amounts on top of the even split, for something one specific person bought/used just for themselves within a larger shared expense (e.g. \"I also grabbed a toothbrush for myself, that's $2.25\"). That person still belongs in splitMembers too, unless the user separately excludes them from the shared portion. Leave this empty if nothing like that was mentioned.",
              "items": {
                "type": "object",
                "properties": {
                  "memberName": { "type": "string" },
                  "amount": { "type": "number" }
                },
                "required": ["memberName", "amount"]
              }
            },
            "editExpenseId": {
              "type": ["string", "null"],
              "description": "Set to the exact id string of one of the expenses listed under 'Expenses you can edit' if the user is asking to correct/re-split/change something already logged there, instead of describing a brand-new expense. If the reference could plausibly match more than one of those expenses, do not guess: leave this null, set needsClarification=true, and list the ambiguous candidates (description + amount) in clarificationQuestion so the user can say which one. Leave null entirely for a new expense."
            },
            "needsClarification": { "type": "boolean" },
            "clarificationQuestion": { "type": "string", "description": "The follow-up question to ask when information is missing" }
          },
          "required": ["description", "totalAmount", "paidBy", "splitMembers", "personalItems", "needsClarification"]
        }
        """;

    private const string AddMemberParametersJson = """
        {
          "type": "object",
          "properties": {
            "displayName": { "type": "string", "description": "The name of the person to add to the group" }
          },
          "required": ["displayName"]
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

    public async Task<AiChatParseResult> ParseAsync(
        IReadOnlyList<string> memberNames,
        IReadOnlyList<EditableExpenseContext> editableExpenses,
        IReadOnlyList<AiChatMessageDto> conversation,
        CancellationToken ct = default)
    {
        _chatClient ??= new ChatClient(_options.Value.Model, _options.Value.ApiKey);

        var messages = new List<ChatMessage> { new SystemChatMessage(BuildSystemPrompt(memberNames, editableExpenses)) };
        foreach (var turn in conversation)
        {
            messages.Add(turn.Role == "assistant"
                ? new AssistantChatMessage(turn.Content)
                : new UserChatMessage(turn.Content));
        }

        var options = new ChatCompletionOptions
        {
            Tools =
            {
                ChatTool.CreateFunctionTool(LogExpenseToolName, "Records or edits a group expense entry in structured form", BinaryData.FromString(LogExpenseParametersJson)),
                ChatTool.CreateFunctionTool(AddMemberToolName, "Adds a new person to the group by name", BinaryData.FromString(AddMemberParametersJson)),
            },
            // Required (not forced to a single named tool) so the model can call log_expense,
            // add_member, or both in the same turn — e.g. "add Anthony and split the cinema
            // bill three ways" needs one call to each. AllowParallelToolCalls is what lets more
            // than one come back in a single completion.
            ToolChoice = ChatToolChoice.CreateRequiredChoice(),
            AllowParallelToolCalls = true,
        };

        var completion = await _chatClient.CompleteChatAsync(messages, options, ct);
        if (completion.Value.ToolCalls.Count == 0)
        {
            throw new InvalidOperationException("The AI did not call any tool.");
        }

        var membersToAdd = new List<string>();
        LogExpenseToolResult? expense = null;

        foreach (var toolCall in completion.Value.ToolCalls)
        {
            if (toolCall.FunctionName == AddMemberToolName)
            {
                var addArgs = JsonSerializer.Deserialize<AddMemberArgs>(toolCall.FunctionArguments, JsonOptions)
                    ?? throw new InvalidOperationException("Could not parse the AI's add_member arguments.");
                membersToAdd.Add(addArgs.DisplayName);
            }
            else if (toolCall.FunctionName == LogExpenseToolName && expense is null)
            {
                var args = JsonSerializer.Deserialize<LogExpenseArgs>(toolCall.FunctionArguments, JsonOptions)
                    ?? throw new InvalidOperationException("Could not parse the AI's log_expense arguments.");

                expense = new LogExpenseToolResult(
                    args.Description,
                    args.TotalAmount,
                    args.PaidBy,
                    args.SplitMembers,
                    args.PersonalItems.Select(p => new LogExpensePersonalItem(p.MemberName, p.Amount)).ToList(),
                    args.NeedsClarification,
                    args.ClarificationQuestion,
                    args.EditExpenseId);
            }
        }

        return new AiChatParseResult(membersToAdd, expense);
    }

    private static string BuildSystemPrompt(IReadOnlyList<string> memberNames, IReadOnlyList<EditableExpenseContext> editableExpenses) => $"""
        You are the AI assistant for a group expense-splitting app.
        Analyze the user's natural-language input and call the appropriate tool(s):
        - log_expense to record a new expense or edit one already logged.
        - add_member to add a new person to the group by name.
        Call both in the same turn if the message asks for both (e.g. "add Anthony and split
        the cinema bill between us all") — add_member first conceptually, then log_expense can
        refer to that new person by name; you don't need to wait for a separate turn.
        You must call at least one tool every turn.

        log_expense rules:
        - Group members: {string.Join(", ", memberNames)}
        - If the amount or who paid is unclear, set needsClarification=true and state exactly what's missing.
        - If the message doesn't make clear whether this should be split among the group or is a
          personal expense charged to one person, don't guess either way — set
          needsClarification=true and ask something like "Should I split this evenly among
          everyone, or was this just for you?"
        - Only skip that question when the message already makes the split obvious — either by
          naming who's involved/excluded (e.g. "split between me and Bob", "not for Carol"), or
          by clearly describing something personal (e.g. "just for myself, don't split it").
        - Never do division or rounding math yourself. Put everyone who evenly shares the expense
          in splitMembers and let the app divide the remaining amount for you. If someone also
          has their own extra item folded into the total (e.g. "I also grabbed a toothbrush for
          myself, that's $2.25"), add it to personalItems as an amount on top of their even
          share — do not subtract it from splitMembers or treat it as replacing their share.
        - If no currency is mentioned, assume the group's default currency.

        {BuildEditableExpensesBlock(editableExpenses)}
        - If the user is asking to correct, re-split, or otherwise change one of the expenses
          listed above (not describe a brand-new one), set editExpenseId to its id and use its
          listed totalAmount/paidBy/split as your starting point — only change what the user
          explicitly asked to change, carrying everything else over unchanged. If it's ambiguous
          which listed expense they mean, ask instead of guessing (see editExpenseId's
          description). Any expense not listed above cannot be edited by this user — if they
          seem to be referring to one, treat it as if you don't know its details and ask for
          them, the same as a new expense.

        add_member rules:
        - Only call this when the message clearly asks to add a specific named person who isn't
          already in the group list above. Don't call it speculatively.
        - Added members join as an unclaimed placeholder (like clicking "add someone by name" in
          the app) — no confirmation step needed, just add them.
        """;

    private static string BuildEditableExpensesBlock(IReadOnlyList<EditableExpenseContext> editableExpenses)
    {
        if (editableExpenses.Count == 0)
        {
            return "Expenses you can edit: none yet.";
        }

        var lines = editableExpenses.Select(e =>
        {
            var shares = string.Join(", ", e.Shares.Select(s => $"{s.MemberName} ${s.Amount:0.00}"));
            return $"- id={e.Id} \"{e.Description}\" total=${e.TotalAmount:0.00}, paid by {e.PaidByName}, split: {shares}";
        });

        return "Expenses you can edit (most recent first):\n" + string.Join("\n", lines);
    }

    private record LogExpenseArgs(
        string Description,
        decimal TotalAmount,
        string PaidBy,
        List<string> SplitMembers,
        List<LogExpensePersonalItemJson> PersonalItems,
        bool NeedsClarification,
        string? ClarificationQuestion,
        string? EditExpenseId = null);

    private record LogExpensePersonalItemJson(string MemberName, decimal Amount);

    private record AddMemberArgs(string DisplayName);
}
