namespace OmniBot.Common
{
    public enum PersonGender
    {
        Neutral,
        Male,
        Female
    }

    public class Person
    {
        public string Name { get; set; } = "User";
        public string[] Surnames { get; set; }
        public PersonGender Gender { get; set; } = PersonGender.Neutral;
    }
}