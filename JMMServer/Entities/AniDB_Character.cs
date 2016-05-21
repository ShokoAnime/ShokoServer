using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI;
using JMMContracts;
using JMMContracts.KodiContracts;
using JMMServer.Repositories;
using System.IO;
using JMMServer.ImageDownload;
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

		public Contract_AniDB_Character ToContract(AniDB_Anime_Character charRel)
		{
			Contract_AniDB_Character contract = new Contract_AniDB_Character();

			contract.AniDB_CharacterID = this.AniDB_CharacterID;
			contract.CharID = this.CharID;
			contract.PicName = this.PicName;
			contract.CreatorListRaw = this.CreatorListRaw;
			contract.CharName = this.CharName;
			contract.CharKanjiName = this.CharKanjiName;
			contract.CharDescription = this.CharDescription;

			contract.CharType = charRel.CharType;

			contract.Seiyuu = null;
			AniDB_Seiyuu seiyuu = this.GetSeiyuu();
			if (seiyuu != null)
				contract.Seiyuu = seiyuu.ToContract();

			return contract;
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

			contract.ImageType = (int)JMMImageType.AniDB_Character;
			contract.ImageID = this.AniDB_CharacterID;

			AniDB_Seiyuu seiyuu = this.GetSeiyuu(session);
			if (seiyuu != null)
			{
				contract.SeiyuuID = seiyuu.AniDB_SeiyuuID;
				contract.SeiyuuName = seiyuu.SeiyuuName;
				contract.SeiyuuImageType = (int)JMMImageType.AniDB_Creator;
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

        public JMMContracts.KodiContracts.Character toContractKodi()
        {
            Character c = new Character();
            c.CharID = this.CharID;
            c.CharName = this.CharName;
            c.Description = this.CharDescription;
            c.Picture = "2/" + c.CharID;
            AniDB_Seiyuu seiyuu_tmp = GetSeiyuu();
            if (seiyuu_tmp != null)
            {
                c.SeiyuuName = seiyuu_tmp.SeiyuuName;
                c.SeiyuuPic = "3/" + seiyuu_tmp.AniDB_SeiyuuID;
            }
            else
            {
                c.SeiyuuName = "";
                c.SeiyuuPic = "";
            }

            return c;
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
			AniDB_Character_SeiyuuRepository repCharSeiyuu = new AniDB_Character_SeiyuuRepository();
			List<AniDB_Character_Seiyuu> charSeiyuus = repCharSeiyuu.GetByCharID(session, CharID);

			if (charSeiyuus.Count > 0)
			{
				// just use the first creator
				AniDB_SeiyuuRepository repCreators = new AniDB_SeiyuuRepository();
				return repCreators.GetBySeiyuuID(session, charSeiyuus[0].SeiyuuID);
			}

			return null;
		}
	}
}
