namespace OmniBot.Common
{
    public class BaseBotOptions
    {
        public string DefaultLanguageCode { get; set; }
        public Language DefaultLanguage => Language.GetByIetfTag(DefaultLanguageCode);
    }
}
