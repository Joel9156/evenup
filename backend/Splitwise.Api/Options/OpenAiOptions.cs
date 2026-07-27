namespace Splitwise.Api.Options;

public class OpenAiOptions
{
    public const string SectionName = "OpenAi";

    // Provided via user-secrets locally / environment variable in deployment — never committed.
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-4o-mini";
}
