using System.Collections.Generic;
using System.IO;
using AniDBAPI;
using NHibernate;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.ImageDownload;
using Shoko.Server.Repositories;

namespace Shoko.Server.Entities
{
    public class SVR_AniDB_Character : AniDB_Character
    {
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

        public CL_AniDB_Character ToClient(string charType, SVR_AniDB_Seiyuu seiyuu)
        {
            CL_AniDB_Character contract = this.CloneToClient();
            if (seiyuu != null)
            {
                contract.Seiyuu = seiyuu;
            }

            return contract;
        }

        public CL_AniDB_Character ToClient(string charType)
        {
            SVR_AniDB_Seiyuu seiyuu = GetSeiyuu();

            return ToClient(charType, seiyuu);
        }

        public MetroContract_AniDB_Character ToContractMetro(ISession session, SVR_AniDB_Anime_Character charRel)
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

            SVR_AniDB_Seiyuu seiyuu = this.GetSeiyuu(session);
            if (seiyuu != null)
            {
                contract.SeiyuuID = seiyuu.AniDB_SeiyuuID;
                contract.SeiyuuName = seiyuu.SeiyuuName;
                contract.SeiyuuImageType = (int) JMMImageType.AniDB_Creator;
                contract.SeiyuuImageID = seiyuu.AniDB_SeiyuuID;
            }

            return contract;
        }

        public Shoko.Models.Azure.Azure_AnimeCharacter ToContractAzure(SVR_AniDB_Anime_Character charRel)
        {
            Shoko.Models.Azure.Azure_AnimeCharacter contract = new Shoko.Models.Azure.Azure_AnimeCharacter();

            contract.CharID = this.CharID;
            contract.CharName = this.CharName;
            contract.CharKanjiName = this.CharKanjiName;
            contract.CharDescription = this.CharDescription;
            contract.CharType = charRel.CharType;
            contract.CharImageURL = string.Format(Constants.URLS.AniDB_Images, PicName);

            SVR_AniDB_Seiyuu seiyuu = this.GetSeiyuu();
            if (seiyuu != null)
            {
                contract.SeiyuuID = seiyuu.AniDB_SeiyuuID;
                contract.SeiyuuName = seiyuu.SeiyuuName;
                contract.SeiyuuImageURL = string.Format(Constants.URLS.AniDB_Images, seiyuu.PicName);
            }

            return contract;
        }

        public SVR_AniDB_Seiyuu GetSeiyuu()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetSeiyuu(session);
            }
        }

        public SVR_AniDB_Seiyuu GetSeiyuu(ISession session)
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