using System;
using System.Collections.Generic;
using System.IO;
using AniDBAPI;
using JMMContracts;
using JMMServer.Databases;
using JMMServer.ImageDownload;
using JMMServer.Repositories;
using JMMServer.Repositories.Direct;
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
            this.CharID = rawChar.CharID;
            this.CharDescription = rawChar.CharDescription;
            this.CharKanjiName = rawChar.CharKanjiName;
            this.CharName = rawChar.CharName;
            this.PicName = rawChar.PicName;
            this.CreatorListRaw = rawChar.CreatorListRaw;
        }

        public void PopulateFromHTTP(Raw_AniDB_Character rawChar)
        {
            if (this.CharID == 0) // a new object
            {
                Populate(rawChar);
            }
            else
            {
                // only update the fields that come from HTTP API
                this.CharDescription = rawChar.CharDescription;
                this.CharName = rawChar.CharName;
                this.CreatorListRaw = rawChar.CreatorListRaw;
                this.PicName = rawChar.PicName;
            }
        }

        public void PopulateFromUDP(Raw_AniDB_Character rawChar)
        {
            if (this.CharID == 0) // a new object
            {
                Populate(rawChar);
            }
            else
            {
                // only update the fields that com from UDP API
                this.CharKanjiName = rawChar.CharKanjiName;
                this.CharName = rawChar.CharName;
                //this.CreatorListRaw = rawChar.CreatorListRaw;
            }
        }

        public Contract_AniDB_Character ToContract(string charType, AniDB_Seiyuu seiyuu)
        {
            var contract = new Contract_AniDB_Character
                {
                    AniDB_CharacterID = AniDB_CharacterID,
                    CharID = CharID,
                    PicName = PicName,
                    CreatorListRaw = CreatorListRaw,
                    CharName = CharName,
                    CharKanjiName = CharKanjiName,
                    CharDescription = CharDescription,
                    CharType = charType
                };

            if (seiyuu != null)
            {
                contract.Seiyuu = seiyuu.ToContract();
            }

            return contract;
        }

        public Contract_AniDB_Character ToContract(string charType)
        {
            AniDB_Seiyuu seiyuu = GetSeiyuu();

            return ToContract(charType, seiyuu);
        }

        public MetroContract_AniDB_Character ToContractMetro(ISession session, AniDB_Anime_Character charRel)
        {
            MetroContract_AniDB_Character contract = new MetroContract_AniDB_Character();

            contract.AniDB_CharacterID = this.AniDB_CharacterID;
            contract.CharID = this.CharID;
            contract.CharName = this.CharName;
            contract.CharKanjiName = this.CharKanjiName;
            contract.CharDescription = this.CharDescription;

            contract.CharType = charRel.CharType;

            contract.ImageType = (int) JMMImageType.AniDB_Character;
            contract.ImageID = this.AniDB_CharacterID;

            AniDB_Seiyuu seiyuu = this.GetSeiyuu(session);
            if (seiyuu != null)
            {
                contract.SeiyuuID = seiyuu.AniDB_SeiyuuID;
                contract.SeiyuuName = seiyuu.SeiyuuName;
                contract.SeiyuuImageType = (int) JMMImageType.AniDB_Creator;
                contract.SeiyuuImageID = seiyuu.AniDB_SeiyuuID;
            }

            return contract;
        }

        public JMMServer.Providers.Azure.AnimeCharacter ToContractAzure(AniDB_Anime_Character charRel)
        {
            JMMServer.Providers.Azure.AnimeCharacter contract = new JMMServer.Providers.Azure.AnimeCharacter();

            contract.CharID = this.CharID;
            contract.CharName = this.CharName;
            contract.CharKanjiName = this.CharKanjiName;
            contract.CharDescription = this.CharDescription;
            contract.CharType = charRel.CharType;
            contract.CharImageURL = string.Format(Constants.URLS.AniDB_Images, PicName);

            AniDB_Seiyuu seiyuu = this.GetSeiyuu();
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
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetSeiyuu(session);
            }
        }

        public AniDB_Seiyuu GetSeiyuu(ISession session)
        {
            List<AniDB_Character_Seiyuu> charSeiyuus = RepoFactory.AniDB_Character_Seiyuu.GetByCharID(session, CharID);

            if (charSeiyuus.Count > 0)
            {
                // just use the first creator
                return RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(session, charSeiyuus[0].SeiyuuID);
            }

            return null;
        }
    }
}