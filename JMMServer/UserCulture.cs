using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
                userLanguages.Add(new UserCulture("de", "German", @"Images/Flags/de_germany.gif"));

                return userLanguages;
            }
        }

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


    }
}
