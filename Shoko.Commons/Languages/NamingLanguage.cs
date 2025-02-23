
namespace Shoko.Commons.Languages
{
    public class NamingLanguage
    {
        public string Language { get; set; }

        public string FlagImage => Languages.GetFlagImage(Language.Trim().ToUpper());

        public string LanguageDescription => Languages.GetLanguageDescription(Language.Trim().ToUpper());

        public NamingLanguage()
        {
        }

        public NamingLanguage(string language)
        {
            Language = language;
        }

        public override string ToString()
        {
            return $"{Language} - ({LanguageDescription}) - {FlagImage}";
        }
    }
}
