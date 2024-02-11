using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;

namespace OmniBot.OpenAI.ChatCompletion;

public class ChatCompletionClient
{
    private string _apiKey;
    private string _apiEndpoint;
    private string _model;

    public ChatCompletionClient(string apiKey, string model) : this(apiKey, model, "https://api.openai.com/v1") { }
    public ChatCompletionClient(string apiKey, string model, string apiEndpoint)
    {
        _apiKey = apiKey;
        _model = model;
        _apiEndpoint = apiEndpoint;
    }

    public async Task<ChatMessage> ProcessMessages(IEnumerable<ChatMessage> messages)
    {
        await Task.Yield();
        return messages.Last();

        ChatCompletionCreateRequest chatCompletionRequest = new ChatCompletionCreateRequest()
        {
            Model = _model,
            Messages = messages.ToList()
        };

        HttpClient httpClient = new HttpClient();

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var jsonSerializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };

        var httpResponse = await httpClient.PostAsJsonAsync($"{_apiEndpoint}/chat/completions", chatCompletionRequest, jsonSerializerOptions);
        var response = await httpResponse.Content.ReadFromJsonAsync<ChatCompletionCreateResponse>();

        if (!response.Successful)
            throw new Exception(response.Error.Message);

        return response.Choices.FirstOrDefault()?.Message;
    }
}
