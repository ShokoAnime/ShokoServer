using AniDBAPI;
using JMMContracts;
using JMMServer.Repositories;
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
            CharID = rawChar.CharID;
            AnimeID = rawChar.AnimeID;
            CharType = rawChar.CharType;
            EpisodeListRaw = rawChar.EpisodeListRaw;
        }

        public AniDB_Character GetCharacter()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetCharacter(session);
            }
        }

        public AniDB_Character GetCharacter(ISession session)
        {
            var repChar = new AniDB_CharacterRepository();
            return repChar.GetByCharID(session, CharID);
        }

        public Contract_AniDB_Anime_Character ToContract()
        {
            var contract = new Contract_AniDB_Anime_Character();

            contract.AniDB_Anime_CharacterID = AniDB_Anime_CharacterID;
            contract.AnimeID = AnimeID;
            contract.CharID = CharID;
            contract.CharType = CharType;
            contract.EpisodeListRaw = EpisodeListRaw;

            return contract;
        }
    }
}