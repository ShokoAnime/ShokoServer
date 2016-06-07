namespace JMMServer
{
    public class NamingLanguage
    {
        public NamingLanguage()
        {
        }

        public NamingLanguage(string language)
        {
            Language = language;
        }

        public string Language { get; set; }

        public string LanguageDescription
        {
            get { return Languages.GetLanguageDescription(Language.Trim().ToUpper()); }
        }

        public override string ToString()
        {
            return string.Format("{0} - ({1})", Language, LanguageDescription);
        }
    }
}