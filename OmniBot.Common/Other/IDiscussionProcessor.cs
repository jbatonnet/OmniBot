namespace OmniBot.Common
{
    public interface IDiscussionProcessor
    {
        public Task<string> InitiateDiscussion(Language language = null);
        public Task<string> Process(string message, Language language = null);
    }
}