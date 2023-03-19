using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;

namespace OmniBot.OpenAI.ChatCompletion;

public class ChatCompletionClient
{
    public string Model { get; set; } = "gpt-3.5-turbo";

    private OpenAIService openAiService;

    public ChatCompletionClient(string apiKey, string domain = null)
    {
        OpenAiOptions openAiOptions = new OpenAiOptions()
        {
            ApiKey = apiKey
        };

        if (!string.IsNullOrEmpty(domain))
            openAiOptions.BaseDomain = domain;

        openAiService = new OpenAIService(openAiOptions);
    }

    public async Task<ChatMessage> ProcessMessages(IEnumerable<ChatMessage> messages)
    {
        ChatCompletionCreateRequest chatCompletionRequest = new ChatCompletionCreateRequest()
        {
            Model = Model,
            Messages = messages.ToList()
        };

        var response = await openAiService.CreateCompletion(chatCompletionRequest);
        if (!response.Successful)
            throw new Exception(response.Error.Message);

        return response.Choices.FirstOrDefault()?.Message;
    }
}
