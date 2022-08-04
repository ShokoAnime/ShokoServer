using System.Collections.Generic;

namespace Shoko.Server.Utilities
{
    public static class LanguageExtensions
    {
        public static bool ContainsOnlyLatin(this List<NamingLanguage> languages)
        {
            foreach (NamingLanguage nlan in languages)
            {
                if (!nlan.IsLatin)
                {
                    return false;
                }
            }

            return true;
        }

    }
}