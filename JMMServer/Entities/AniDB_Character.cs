using System.IO;
using AniDBAPI;
using JMMContracts;
using JMMServer.ImageDownload;
using JMMServer.Providers.Azure;
using JMMServer.Repositories;
using NHibernate;

namespace JMMServer.Entities
{
    public class AniDB_Character
    {
        public int AniDB_CharacterID { get; private set; }
        public int CharID { get; set; }
        public string PicName { get; set; }
        public string CreatorListRaw { get; set; }
        public string CharName { get; set; }
        public string CharKanjiName { get; set; }
        public string CharDescription { get; set; }

        public string PosterPath
        {
            get
            {
                if (string.IsNullOrEmpty(PicName)) return "";

                return Path.Combine(ImageUtils.GetAniDBCharacterImagePath(CharID), PicName);
            }
        }

        private void Populate(Raw_AniDB_Character rawChar)
        {
            CharID = rawChar.CharID;
            CharDescription = rawChar.CharDescription;
            CharKanjiName = rawChar.CharKanjiName;
            CharName = rawChar.CharName;
            PicName = rawChar.PicName;
            CreatorListRaw = rawChar.CreatorListRaw;
        }

        public void PopulateFromHTTP(Raw_AniDB_Character rawChar)
        {
            if (CharID == 0) // a new object
            {
                Populate(rawChar);
            }
            else
            {
                // only update the fields that come from HTTP API
                CharDescription = rawChar.CharDescription;
                CharName = rawChar.CharName;
                CreatorListRaw = rawChar.CreatorListRaw;
                PicName = rawChar.PicName;
            }
        }

        public void PopulateFromUDP(Raw_AniDB_Character rawChar)
        {
            if (CharID == 0) // a new object
            {
                Populate(rawChar);
            }
            else
            {
                // only update the fields that com from UDP API
                CharKanjiName = rawChar.CharKanjiName;
                CharName = rawChar.CharName;
                //this.CreatorListRaw = rawChar.CreatorListRaw;
            }
        }

        public Contract_AniDB_Character ToContract(AniDB_Anime_Character charRel)
        {
            var contract = new Contract_AniDB_Character();

            contract.AniDB_CharacterID = AniDB_CharacterID;
            contract.CharID = CharID;
            contract.PicName = PicName;
            contract.CreatorListRaw = CreatorListRaw;
            contract.CharName = CharName;
            contract.CharKanjiName = CharKanjiName;
            contract.CharDescription = CharDescription;

            contract.CharType = charRel.CharType;

            contract.Seiyuu = null;
            var seiyuu = GetSeiyuu();
            if (seiyuu != null)
                contract.Seiyuu = seiyuu.ToContract();

            return contract;
        }

        public MetroContract_AniDB_Character ToContractMetro(ISession session, AniDB_Anime_Character charRel)
        {
            var contract = new MetroContract_AniDB_Character();

            contract.AniDB_CharacterID = AniDB_CharacterID;
            contract.CharID = CharID;
            contract.CharName = CharName;
            contract.CharKanjiName = CharKanjiName;
            contract.CharDescription = CharDescription;

            contract.CharType = charRel.CharType;

            contract.ImageType = (int)JMMImageType.AniDB_Character;
            contract.ImageID = AniDB_CharacterID;

            var seiyuu = GetSeiyuu(session);
            if (seiyuu != null)
            {
                contract.SeiyuuID = seiyuu.AniDB_SeiyuuID;
                contract.SeiyuuName = seiyuu.SeiyuuName;
                contract.SeiyuuImageType = (int)JMMImageType.AniDB_Creator;
                contract.SeiyuuImageID = seiyuu.AniDB_SeiyuuID;
            }

            return contract;
        }

        public AnimeCharacter ToContractAzure(AniDB_Anime_Character charRel)
        {
            var contract = new AnimeCharacter();

            contract.CharID = CharID;
            contract.CharName = CharName;
            contract.CharKanjiName = CharKanjiName;
            contract.CharDescription = CharDescription;
            contract.CharType = charRel.CharType;
            contract.CharImageURL = string.Format(Constants.URLS.AniDB_Images, PicName);

            var seiyuu = GetSeiyuu();
            if (seiyuu != null)
            {
                contract.SeiyuuID = seiyuu.AniDB_SeiyuuID;
                contract.SeiyuuName = seiyuu.SeiyuuName;
                contract.SeiyuuImageURL = string.Format(Constants.URLS.AniDB_Images, seiyuu.PicName);
            }

            return contract;
        }

        public AniDB_Seiyuu GetSeiyuu()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetSeiyuu(session);
            }
        }

        public AniDB_Seiyuu GetSeiyuu(ISession session)
        {
            var repCharSeiyuu = new AniDB_Character_SeiyuuRepository();
            var charSeiyuus = repCharSeiyuu.GetByCharID(session, CharID);

            if (charSeiyuus.Count > 0)
            {
                // just use the first creator
                var repCreators = new AniDB_SeiyuuRepository();
                return repCreators.GetBySeiyuuID(session, charSeiyuus[0].SeiyuuID);
            }

            return null;
        }
    }
}