namespace OmniBot.OpenAI;

public class OpenAIOptions
{
    public string ApiEndpoint { get; set; } = "https://api.openai.com/v1";
    public string ApiKey { get; set; }
    public string Model { get; set; }
}
