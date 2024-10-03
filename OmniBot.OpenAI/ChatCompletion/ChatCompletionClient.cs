using System.ClientModel;

using OpenAI;
using OpenAI.Chat;

namespace OmniBot.OpenAI.ChatCompletion;

public class ChatCompletionClient
{
    private string _apiKey;
    private string _apiEndpoint;
    private string _model;

    private OpenAIClient openAIClient;

    public ChatCompletionClient(string apiKey, string model) : this(apiKey, model, "https://api.openai.com/v1") { }
    public ChatCompletionClient(string apiKey, string model, string apiEndpoint)
    {
        _apiKey = apiKey;
        _model = model;
        _apiEndpoint = apiEndpoint;

        var options = new OpenAIClientOptions()
        {
            Endpoint = new Uri(apiEndpoint)
        };

        openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), options);
    }

    public async Task<Message> ProcessMessages(IEnumerable<Message> messages, float temperature = 0.7f)
    {
        var chatClient = openAIClient.GetChatClient(_model);

        var result = await chatClient.CompleteChatAsync
        (
            messages: messages.Select(m => m.ToChatMessage()).ToArray(),
            options: new ChatCompletionOptions()
            {
                Temperature = temperature
            }
        );

        return new Message(MessageRole.Assistant, result.Value?.Content.FirstOrDefault()?.Text);
    }
}
