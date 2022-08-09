using System;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;

namespace Shoko.Server.Utilities
{
    public class NamingLanguage
    {
        public TitleLanguage Language { get; set; }
        
        public string LanguageCode
        {
            get
            {
                return Language.GetString();
            }
        }

        public string LanguageDescription
        {
            get
            {
                return Language.GetDescription();
            }
        }

        public NamingLanguage()
        {
        }

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
}