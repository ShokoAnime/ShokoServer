using AniDBAPI;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Entities
{
    // ReSharper disable once InconsistentNaming
    public class SVR_AniDB_Anime_Character : AniDB_Anime_Character
    {

        public SVR_AniDB_Anime_Character() //Empty Constructor for nhibernate
        {

        }

        public void Populate(Raw_AniDB_Character rawChar)
        {
            CharID = rawChar.CharID;
            AnimeID = rawChar.AnimeID;
            CharType = rawChar.CharType;
            EpisodeListRaw = rawChar.EpisodeListRaw;
        }

        public SVR_AniDB_Character GetCharacter()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetCharacter(session.Wrap());
            }
        }

        public SVR_AniDB_Character GetCharacter(ISessionWrapper session)
        {
            return RepoFactory.AniDB_Character.GetByCharID(session, CharID);
        }

    }
}