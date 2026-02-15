using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;

namespace Shoko.Server.Utilities;

public class NamingLanguage
{
    public TitleLanguage Language { get; set; }

    public string LanguageCode => Language.GetString();

    public string LanguageDescription => Language.GetDescription();

    public NamingLanguage(TitleLanguage language)
    {
        Language = language;
    }

    public NamingLanguage(string language)
    {
        Language = language.GetTitleLanguage();
    }

    public override string ToString()
    {
        return string.Format("{0} - ({1})", Language, LanguageDescription);
    }
}
