using System.Text;

using OmniBot.Common;

using OpenAI_API.Chat;

namespace OmniBot.OpenAI.ChatCompletion
{
    public class GroupChatGptDiscussionProcessor : ChatGptDiscussionProcessor
    {
        public record Person(string name, string[] surnames, int? age, PersonGender gender, string description);

        public PersonGender BotGender { get; set; } = PersonGender.Neutral;
        public string BotName { get; set; } = "Jenny";
        public string[] BotSurnames { get; set; } = new[] { "Jennifer" };
        public byte BotAge { get; set; } = 30;

        public string Location { get; set; } = "a group chat";
        public string AdditionalContext { get; set; }

        public List<Person> Persons { get; set; } = new List<Person>();
        public List<Person> ActivePersons { get; set; } = new List<Person>();

        public bool GreetPersonsEntering { get; set; } = false;

        private Person lastTalkingPerson = null;
        private DateTime lastInteraction = DateTime.MinValue;

        public GroupChatGptDiscussionProcessor(ChatCompletionClient chatCompletionClient) : base(chatCompletionClient)
        {
        }

        public override async Task<string> InitiateDiscussion(Language language = null)
        {
            if (!GreetPersonsEntering)
                return null;

            if (Persons.Count != 1)
                return null;

            Person singlePerson = Persons[0];

            switch (language?.GetTwoLettersCode())
            {
                case "fr":
                    return await ProcessPersonMessage(singlePerson, "Bonjour !", language);
                
                case "en":
                default:
                    return await ProcessPersonMessage(singlePerson, "Hey !", language);
            }
        }

        public async Task<string> ProcessPersonMessage(Person person, string message, Language language = null)
        {
            if (lastTalkingPerson != person)
                lastInteraction = DateTime.MinValue;

            if (lastTalkingPerson != person || (DateTime.Now - lastInteraction) > TimeSpan.FromSeconds(20))
            {
                if (ActivePersons.Count != 1)
                {
                    if (!message.ToLower().Contains(BotName.ToLower()) && !BotSurnames.Any(n => message.ToLower().Contains(n.ToLower())))
                        return null;
                }
            }

            if (lastTalkingPerson != person)
            {
                messageHistory.Add(new ChatMessage(ChatMessageRole.System, $"{person.name} is now talking."));
                lastTalkingPerson = person;
            }

            lastInteraction = DateTime.Now;

            return await Process(message, language);
        }
        public async Task<string> ProcessPersonEntering(Person person, Language language)
        {
            Persons.Add(person);

            messageHistory.Add(new ChatMessage(ChatMessageRole.System, $"{person.name} entered the room and joined the discussion."));
            lastTalkingPerson = null;

            if (!GreetPersonsEntering)
                return null;

            string message = await ProcessInternal(language);
            return message;
        }
        public async Task<string> ProcessPersonLeaving(Person person, Language language)
        {
            Persons.Remove(person);

            messageHistory.Add(new ChatMessage(ChatMessageRole.System, $"{person.name} left the discussion and the room."));

            return null;
        }

        protected override List<ChatMessage> PrepareMessageList(Language language)
        {
            StringBuilder contextBuilder = new StringBuilder();

            contextBuilder.Append("I am writing a fictional story about a discussion between friends");
            if (!string.IsNullOrWhiteSpace(Location))
                contextBuilder.Append($" in {Location}");
            contextBuilder.Append(". ");

            foreach (Person person in Persons)
            {
                contextBuilder.Append($"There is {person.name}");

                if (person.age != null)
                    contextBuilder.Append(MessageHelper.ProcessGenderTemplate($", (he|she|they) (is|is|are) {person.age}", person.gender));

                contextBuilder.Append(". ");

                if (person.description != null)
                    contextBuilder.Append($"{person.description}. ");
            }

            contextBuilder.Append(MessageHelper.ProcessGenderTemplate($"Finally, there is {BotName}, (he|she|they) (is|is|are) {BotAge}. ", BotGender));
            contextBuilder.Append($"You will simulate this fictional conversation playing the role of {BotName}. You will use short and precise sentences. ");

            if (!string.IsNullOrWhiteSpace(AdditionalContext))
                contextBuilder.Append(AdditionalContext);

            string context = contextBuilder.ToString();

            List<ChatMessage> messages = base.PrepareMessageList(language);

            messages.Insert(0, new ChatMessage(ChatMessageRole.System, context));

            return messages;
        }
    }
}