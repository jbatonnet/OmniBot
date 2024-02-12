namespace OmniBot.Common.Conversation;

public interface IConversationProcessor
{
    public Task<Event> ProcessConversation(Person person, IEnumerable<Event> interactionHistory);
}
