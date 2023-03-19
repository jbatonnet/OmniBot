namespace OmniBot.Common.Conversation;

public interface IConversationProcessor
{
    public Task<Event> ProcessConversation(Person member, List<Event> interactionHistory);
}
