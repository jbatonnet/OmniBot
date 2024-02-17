using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Models;

namespace OmniBot.OpenAI.ChatCompletion;

public class ChatCompletionClient
{
    private string _apiKey;
    private string _apiEndpoint;
    private string _model;

    private OpenAIAPI openAiApi;

    public ChatCompletionClient(string apiKey, string model) : this(apiKey, model, "https://api.openai.com/v1") { }
    public ChatCompletionClient(string apiKey, string model, string apiEndpoint)
    {
        _apiKey = apiKey;
        _model = model;
        _apiEndpoint = apiEndpoint;

        openAiApi = new OpenAIAPI()
        {
            ApiUrlFormat = apiEndpoint + "/{1}",
            Auth = new APIAuthentication(apiKey)
        };
    }

    public async Task<ChatMessage> ProcessMessages(IEnumerable<ChatMessage> messages, float temperature = 0.7f)
    {
        var result = await openAiApi.Chat.CreateChatCompletionAsync
        (
            messages: messages.ToArray(),
            model: new Model(_model),
            temperature: temperature
        );

        return result.Choices.FirstOrDefault()?.Message;
    }
}
