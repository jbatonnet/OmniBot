using OmniBot.Common;

using OpenAI.ObjectModels.RequestModels;

namespace OmniBot.OpenAI.ChatCompletion
{
    public class AssistantChatGptDiscussionProcessor : ChatGptDiscussionProcessor
    {
        public AssistantChatGptDiscussionProcessor(string apiKey) : base(apiKey)
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

        protected override List<ChatMessage> PrepareMessageList(Language language)
        {
            List<ChatMessage> messages = base.PrepareMessageList(language);

            messages.Insert(0, ChatMessage.FromSystem("You are a polite and helpful assistant"));

            return messages;
        }
    }
}