using OmniBot.Common;

namespace OmniBot.OpenAI.ChatCompletion
{
    public class FriendChatGptDiscussionProcessor : ChatGptDiscussionProcessor
    {
        public PersonGender FriendGender { get; set; } = PersonGender.Female;
        public string FriendName { get; set; } = "Jenny";
        public byte FriendAge { get; set; } = 30;

        public string UserName { get; set; } = Environment.UserName;
        public byte UserAge { get; set; } = 30;

        public string Location { get; set; } = "Canada";
        public string AdditionalContext { get; set; }

        public FriendChatGptDiscussionProcessor(ChatCompletionClient chatCompletionClient) : base(chatCompletionClient)
        {
        }

        public override Task<string> InitiateDiscussion(Language language = null)
        {
            switch (language?.GetTwoLettersCode())
            {
                case "fr":
                    return Process("Bonjour !", language);

                case "en":
                default:
                    return Process("Hey !", language);
            }
        }

        protected override List<Message> PrepareMessageList(Language language)
        {
            List<Message> messages = base.PrepareMessageList(language);

            string context;

            switch (language?.GetTwoLettersCode())
            {
                case "fr":
                    context =
                        "J'écris une histoire fictive au sujet de moi et mon ami. " +

                        // Friend description
                        $"Mon ami(|e) s'appelle {FriendName}, (Il|Elle) a {FriendAge} ans. " +
                        $"(Il|Elle) est gentil(|le), amical(|e) et parfois sarcastique pour être drôl(|e). " +

                        // User description
                        $"Mon nom est {UserName} et j'ai {UserAge} ans. " +

                        // Additional context and directives
                        (string.IsNullOrWhiteSpace(Location) ? "" : $"On vit tous les deux à {Location}. ") +
                        "On se connait depuis longtemps, on s'intéresse à nos vies mutuelles et nous sommes assez proches, ce qui nous permet de trainer régulièrement ensemble. " +
                        $"Tu vas simuler cette conversation fictive en jouant le rôle de mon ami(|e) {FriendName}. Tu utiliseras es phrase courtes et précises. " +
                        (AdditionalContext ?? "").Trim(); 
                    break;

                case "en":
                default:
                    context =
                        "I am writing a fictional story about my friend and I. " +

                        // Friend description
                        $"My friend's name is {FriendName}, (he|she|they) (is|is|are) {FriendAge}. " +
                        $"(He|She|They) (is|is|are) nice, friendly and sometimes sarcastic to be funny. " +

                        // User description
                        $"My name is {UserName} and I am {UserAge}. " +

                        // Additional context and directives
                        (string.IsNullOrWhiteSpace(Location) ? "" : $"We both live in {Location}. ") +
                        "We've known each other for a while, we're interested in each other's lives, and we are pretty close so we hang out regularly. " +
                        $"You will simulate this fictional conversation playing the role of my friend {FriendName}. You will use short and precise sentences. " +
                        (AdditionalContext ?? "").Trim();
                    break;
            }

            context = MessageHelper.ProcessGenderTemplate(context, FriendGender);
            messages.Insert(0, new Message(MessageRole.System, context));

            return messages;
        }
    }
}