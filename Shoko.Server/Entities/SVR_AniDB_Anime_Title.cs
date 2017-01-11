using AniDBAPI;
using Shoko.Models.Server;

namespace Shoko.Server.Entities
{
    public class SVR_AniDB_Anime_Title : AniDB_Anime_Title
    {
        public SVR_AniDB_Anime_Title() //Empty Constructor for nhibernate
        {
            
        }
        public void Populate(Raw_AniDB_Anime_Title rawTitle)
        {
            this.AnimeID = rawTitle.AnimeID;
            this.Language = rawTitle.Language;
            this.Title = rawTitle.Title;
            this.TitleType = rawTitle.TitleType;
        }

        public override string ToString()
        {
            return string.Format("{0} - {1} ({2}) - {3}", AnimeID, TitleType, Language, Title);
        }
    }
}