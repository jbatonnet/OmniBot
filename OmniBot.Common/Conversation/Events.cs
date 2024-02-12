namespace OmniBot.Common
{
    public class Event
    {
        public DateTime Time { get; set; } = DateTime.Now;
    }

    public class Message : Event
    {
        public Person Person { get; set; }
        public string Text { get; set; }
    }
    public class Interruption : Event
    {
        public Person Interuptor { get; set; }
        public Person Interupted { get; set; }
    }
}