using AniDBAPI;
using Shoko.Models.Server;

namespace Shoko.Server.Entities
{
    // ReSharper disable once InconsistentNaming
    public class SVR_AniDB_Anime_Tag : AniDB_Anime_Tag
    {
        public SVR_AniDB_Anime_Tag() //Empty Constructor for nhibernate
        {

        }
        public void Populate(Raw_AniDB_Tag rawTag)
        {
            AnimeID = rawTag.AnimeID;
            TagID = rawTag.TagID;
            Approval = 100;
            Weight = rawTag.Weight;
        }
    }
}