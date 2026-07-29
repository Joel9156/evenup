namespace EvenUp.Api.Options;

public class FrontendOptions
{
    public const string SectionName = "Frontend";

    // Used to build the "view full settlement" share link embedded in settlement messages.
    public string BaseUrl { get; set; } = string.Empty;
}
