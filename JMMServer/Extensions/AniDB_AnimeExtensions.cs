using JMMModels.Childs;

namespace JMMServer.Extensions
{
    public static class AniDB_AnimeExtensions
    {
        public static string GetFormattedTitle(this AniDB_Anime anime)
        {
            foreach (NamingLanguage nlan in Languages.PreferredNamingLanguages)
            {
                string thisLanguage = nlan.Language.Trim().ToUpper();

                // Romaji and English titles will be contained in MAIN and/or OFFICIAL
                // we won't use synonyms for these two languages
                if (thisLanguage.Equals(Constants.AniDBLanguageType.Romaji) || thisLanguage.Equals(Constants.AniDBLanguageType.English))
                {
                    foreach (AniDB_Anime_Title title in anime.Titles)
                    {
                        string titleType = title.Type.Trim().ToUpper();
                        // first try the  Main title
                        if (titleType == Constants.AnimeTitleType.Main.ToUpper() && title.Language.Trim().ToUpper() == thisLanguage) return title.Title;
                    }
                }

                // now try the official title
                foreach (AniDB_Anime_Title title in anime.Titles)
                {
                    string titleType = title.Type.Trim().ToUpper();
                    if (titleType == Constants.AnimeTitleType.Official.ToUpper() && title.Language.Trim().ToUpper() == thisLanguage) return title.Title;
                }

                // try synonyms
                if (ServerSettings.LanguageUseSynonyms)
                {
                    foreach (AniDB_Anime_Title title in anime.Titles)
                    {
                        string titleType = title.Type.Trim().ToUpper();
                        if (titleType == Constants.AnimeTitleType.Synonym.ToUpper() && title.Language.Trim().ToUpper() == thisLanguage) return title.Title;
                    }
                }

            }

            // otherwise just use the main title
            return anime.MainTitle;

        }
    }
}
