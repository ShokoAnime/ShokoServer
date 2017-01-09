using AniDBAPI;
using JMMServer.Databases;
using JMMServer.Repositories;
using JMMServer.Repositories.NHibernate;
using Shoko.Models.Server;

namespace JMMServer.Entities
{
    // ReSharper disable once InconsistentNaming
    public class SVR_AniDB_Anime_Character : AniDB_Anime_Character
    {
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