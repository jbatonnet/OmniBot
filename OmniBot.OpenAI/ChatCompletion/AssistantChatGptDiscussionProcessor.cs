using OmniBot.Common;

namespace OmniBot.OpenAI.ChatCompletion
{
    public class AssistantChatGptDiscussionProcessor : ChatGptDiscussionProcessor
    {
        public AssistantChatGptDiscussionProcessor(ChatCompletionClient chatCompletionClient) : base(chatCompletionClient)
        {
        }

        public override Task<string> InitiateDiscussion(Language language = null)
        {
            switch (language?.GetTwoLettersCode())
            {
                case "fr":
                    return Process("Bonjour", language);

                case "en":
                default:
                    return Process("Hello", language);
            }
        }

        protected override List<Message> PrepareMessageList(Language language)
        {
            List<Message> messages = base.PrepareMessageList(language);

            messages.Insert(0, new Message(MessageRole.System, "You are a polite and helpful assistant"));

            return messages;
        }
    }
}