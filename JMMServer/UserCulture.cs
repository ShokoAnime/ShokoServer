using System.Collections.Generic;

namespace JMMServer
{
    public class UserCulture
    {
        public string LanguageName { get; set; }
        public string Culture { get; set; }
        public string FlagImage { get; set; }

        public UserCulture()
        {
        }

        public UserCulture(string culture, string languagename, string flagimage)
        {
            Culture = culture;
            LanguageName = languagename;
            FlagImage = flagimage;
        }

        public static List<UserCulture> SupportedLanguages
        {
            get
            {
                List<UserCulture> userLanguages = new List<UserCulture>();

                userLanguages.Add(new UserCulture("en", "English (US)", @"Images/Flags/us.gif"));
                userLanguages.Add(new UserCulture("nl", "Dutch (Machine Translated)", @"Images/Flags/nl.gif"));
                userLanguages.Add(new UserCulture("fr", "French (Machine Translated)", @"Images/Flags/fr.gif"));
                userLanguages.Add(new UserCulture("de", "German (Machine Translated)", @"Images/Flags/de_germany.gif"));
                userLanguages.Add(new UserCulture("it", "Italian (Machine Translated)", @"Images/Flags/it.gif"));
                userLanguages.Add(new UserCulture("pl", "Polish (Machine Translated)", @"Images/Flags/pl.gif"));
                userLanguages.Add(new UserCulture("ru", "Russian (Machine Translated)", @"Images/Flags/ru.gif"));
                userLanguages.Add(new UserCulture("es", "Spanish (Machine Translated)", @"Images/Flags/es.gif"));

                return userLanguages;
            }
        }

        /*

        /// The following code causes errors with localization. 
        /// Regardless of the langauge selected, it will show any language strings
        /// in .CS files in the language Windows is in and not the selected language. 

        /// <summary>
        /// This will attempt to get the closest culture/lanaguage match
        /// For example if the user's UI Culture is "de-LU" (German - Luxembourg)
        /// we will first try to find an exact match. If we don't support that, we will then try and
        /// get "de" (German). If that doesn't exist, then we can default to english
        /// </summary>
        /// <param name="culture"></param>
        /// <returns></returns>
        public static string GetClosestMatch(string culture)
        {
            // find exact match
            foreach (UserCulture uc in SupportedLanguages)
            {
                if (uc.Culture.Trim().ToUpper() == culture.Trim().ToUpper())
                {
                    return uc.Culture;
                }
            }

            if (culture.Contains("-"))
            {
                string[] items = culture.Split('-');
                string language = items[0];
                // find base language match
                foreach (UserCulture uc in SupportedLanguages)
                {
                    if (uc.Culture.Contains("-")) continue;

                    if (uc.Culture.Trim().ToUpper() == language.Trim().ToUpper())
                    {
                        return uc.Culture;
                    }
                }
            }

            return "en";
        }

        */
    }
}