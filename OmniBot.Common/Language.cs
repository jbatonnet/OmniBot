using System.Globalization;

namespace OmniBot.Common
{
    public class Language
    {
        public static Language French { get; } = GetByIetfTag("fr-FR");
        public static Language English { get; } = GetByIetfTag("en-US");

        private CultureInfo cultureInfo;

        private Language(CultureInfo cultureInfo)
        {
            this.cultureInfo = cultureInfo;
        }

        public string GetTwoLettersCode() => cultureInfo.TwoLetterISOLanguageName;
        public string GetIetfTag() => cultureInfo.IetfLanguageTag;
        public string GetFullname() => cultureInfo.EnglishName;

        public static Language GetByIetfTag(string ietfTag)
        {
            try
            {
                var cultureInfo = CultureInfo.GetCultureInfoByIetfLanguageTag(ietfTag);
                return cultureInfo == null ? null : new Language(cultureInfo);
            }
            catch
            {
                return null;
            }
        }

        public override string ToString() => GetIetfTag();
        public override bool Equals(object obj)
        {
            if (obj is Language language)
                return this == language;

            return false;
        }

        public static bool operator ==(Language left, Language right)
        {
            return left?.GetIetfTag() == right?.GetIetfTag();
        }
        public static bool operator !=(Language left, Language right)
        {
            return left?.GetIetfTag() != right?.GetIetfTag();
        }

        public static implicit operator Language(string ietfTag)
        {
            return GetByIetfTag(ietfTag);
        }
    }
}