namespace OmniBot.OpenAI;

public class OpenAIOptions
{
    public const string OpenAIEndpoint = "https://api.openai.com/v1";

    public string ApiEndpoint { get; set; } = OpenAIEndpoint;
    public string ApiKey { get; set; }
    public string Model { get; set; }
}
