using System.Text.RegularExpressions;

using OmniBot.Common;

using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;

namespace OmniBot.OpenAI.ChatCompletion
{
    public abstract class ChatGptDiscussionProcessor : IDiscussionProcessor
    {
        public enum Gender
        {
            Neutral,
            Male,
            Female
        }

        public bool AddDateTimeInformation { get; set; } = true;

        protected List<ChatMessage> messageHistory = new List<ChatMessage>();

        private OpenAIService openAiService;

        public ChatGptDiscussionProcessor(string apiKey)
        {
            OpenAiOptions openAiOptions = new OpenAiOptions();
            openAiOptions.ApiKey = apiKey;

            openAiService = new OpenAIService(openAiOptions);
        }

        public virtual Task<string> InitiateDiscussion(Language language = null)
        {
            return Process("Hey !", language);
        }
        public virtual Task<string> Process(string message, Language language = null)
        {
            messageHistory.Add(ChatMessage.FromUser(message));
            return ProcessInternal(language);
        }
        public void Reset()
        {
            messageHistory.Clear();
        }

        protected virtual List<ChatMessage> PrepareMessageList(Language language)
        {
            List<ChatMessage> messages = new List<ChatMessage>();

            if (AddDateTimeInformation)
            {
                switch (language?.GetTwoLettersCode())
                {
                    case "fr":
                        //messages.Add(ChatMessage.FromSystem($"On est {DateTime.Now:dddd MMMM d}th of {DateTime.Now:yyyy} and it is {DateTime.Now.ToShortTimeString()}"));
                        break;

                    case "en":
                    default:
                        messages.Add(ChatMessage.FromSystem($"We are {DateTime.Now:dddd MMMM d}th of {DateTime.Now:yyyy} and it is {DateTime.Now.ToShortTimeString()}"));
                        break;
                }
            }

            messages.AddRange(messageHistory);

            return messages;
        }
        protected async Task<string> ProcessInternal(Language language)
        {
            ChatCompletionCreateRequest chatCompletionRequest = new ChatCompletionCreateRequest()
            {
                Model = "gpt-3.5-turbo",
                Messages = PrepareMessageList(language)
            };

            var response = await openAiService.ChatCompletion.CreateCompletion(chatCompletionRequest);
            if (!response.Successful)
                throw new Exception(response.Error.Message);

            string message = response.Choices.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(message))
                return null;

            messageHistory.Add(ChatMessage.FromAssistant(message));

            return message;
        }

        protected static string ProcessGenderTemplate(string template, Gender gender)
        {
            return Regex.Replace(template, @"\(([^|]*)\|([^|\)]*)(?:\|([^)])*)?\)", m => gender switch
            {
                Gender.Male => m.Groups[1].Value,
                Gender.Female => m.Groups[2].Value,
                Gender.Neutral => m.Groups.Count == 4 ? m.Groups[3].Value : m.Groups[1].Value
            });
        }
        protected static string Genderify(string template, Gender gender) => ProcessGenderTemplate(template, gender);
    }
}