using OmniBot.Common;

using OpenAI_API.Chat;

namespace OmniBot.OpenAI.ChatCompletion
{
    public abstract class ChatGptDiscussionProcessor : IDiscussionProcessor
    {
        public bool AddDateTimeInformation { get; set; } = true;

        private readonly ChatCompletionClient _chatCompletionClient;

        protected List<ChatMessage> messageHistory = new List<ChatMessage>();

        public ChatGptDiscussionProcessor(ChatCompletionClient chatCompletionClient)
        {
            _chatCompletionClient = chatCompletionClient;
        }

        public virtual Task<string> InitiateDiscussion(Language language = null)
        {
            return Process("Hey !", language);
        }
        public virtual Task<string> Process(string message, Language language = null)
        {
            messageHistory.Add(new ChatMessage(ChatMessageRole.User, message));
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
                        messages.Add(new ChatMessage(ChatMessageRole.System, $"We are {DateTime.Now:dddd MMMM d}th of {DateTime.Now:yyyy} and it is {DateTime.Now.ToShortTimeString()}"));
                        break;
                }
            }

            messages.AddRange(messageHistory);

            return messages;
        }
        protected async Task<string> ProcessInternal(Language language)
        {
            var message = await _chatCompletionClient.ProcessMessages(PrepareMessageList(language));

            messageHistory.Add(new ChatMessage(ChatMessageRole.Assistant, message.TextContent));

            return message.TextContent;
        }
    }
}
