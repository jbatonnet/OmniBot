using System.Text.RegularExpressions;

namespace OmniBot.Common;

public static class MessageHelper
{
    public static string ProcessGenderTemplate(string template, PersonGender gender)
    {
        return Regex.Replace(template, @"\(([^|]*)\|([^|\)]*)(?:\|([^)])*)?\)", m => gender switch
        {
            PersonGender.Male => m.Groups[1].Value,
            PersonGender.Female => m.Groups[2].Value,
            PersonGender.Neutral => m.Groups.Count == 4 ? m.Groups[3].Value : m.Groups[1].Value
        });
    }
    public static string Genderify(string template, PersonGender gender) => ProcessGenderTemplate(template, gender);
}
