using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI;
using JMMContracts;
using JMMServer.Repositories;
using System.IO;
using JMMServer.ImageDownload;

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
			AniDB_Seiyuu seiyuu = this.Seiyuu;
			if (seiyuu != null)
				contract.Seiyuu = seiyuu.ToContract();

			return contract;
		}

		public MetroContract_AniDB_Character ToContractMetro(AniDB_Anime_Character charRel)
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

			return contract;
		}

		public AniDB_Seiyuu Seiyuu
		{
			get
			{
				AniDB_Character_SeiyuuRepository repCharSeiyuu = new AniDB_Character_SeiyuuRepository();
				List<AniDB_Character_Seiyuu> charSeiyuus = repCharSeiyuu.GetByCharID(CharID);

				if (charSeiyuus.Count > 0)
				{
					// just use the first creator
					AniDB_SeiyuuRepository repCreators = new AniDB_SeiyuuRepository();
					return repCreators.GetBySeiyuuID(charSeiyuus[0].SeiyuuID);
				}

				return null;
			}
		}
	}
}
