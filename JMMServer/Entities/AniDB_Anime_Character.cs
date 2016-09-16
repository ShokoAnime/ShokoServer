using AniDBAPI;
using JMMContracts;
using JMMServer.Repositories;
using JMMServer.Repositories.Direct;
using JMMServer.Repositories.NHibernate;
using NHibernate;

namespace JMMServer.Entities
{
    public class AniDB_Anime_Character
    {
        public int AniDB_Anime_CharacterID { get; private set; }
        public int AnimeID { get; set; }
        public int CharID { get; set; }
        public string CharType { get; set; }
        public string EpisodeListRaw { get; set; }

        public void Populate(Raw_AniDB_Character rawChar)
        {
            this.CharID = rawChar.CharID;
            this.AnimeID = rawChar.AnimeID;
            this.CharType = rawChar.CharType;
            this.EpisodeListRaw = rawChar.EpisodeListRaw;
        }

        public AniDB_Character GetCharacter()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetCharacter(session.Wrap());
            }
        }

        public AniDB_Character GetCharacter(ISessionWrapper session)
        {
            return RepoFactory.AniDB_Character.GetByCharID(session, CharID);
        }

        public Contract_AniDB_Anime_Character ToContract()
        {
            Contract_AniDB_Anime_Character contract = new Contract_AniDB_Anime_Character();

            contract.AniDB_Anime_CharacterID = this.AniDB_Anime_CharacterID;
            contract.AnimeID = this.AnimeID;
            contract.CharID = this.CharID;
            contract.CharType = this.CharType;
            contract.EpisodeListRaw = this.EpisodeListRaw;

            return contract;
        }
    }
}