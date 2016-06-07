using AniDBAPI;

namespace JMMServer.Entities
{
    public class AniDB_Anime_Title
    {
        public int AniDB_Anime_TitleID { get; private set; }
        public int AnimeID { get; set; }
        public string TitleType { get; set; }
        public string Language { get; set; }
        public string Title { get; set; }

        public void Populate(Raw_AniDB_Anime_Title rawTitle)
        {
            AnimeID = rawTitle.AnimeID;
            Language = rawTitle.Language;
            Title = rawTitle.Title;
            TitleType = rawTitle.TitleType;
        }

        public override string ToString()
        {
            return string.Format("{0} - {1} ({2}) - {3}", AnimeID, TitleType, Language, Title);
        }
    }
}