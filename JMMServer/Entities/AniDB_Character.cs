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

			AniDB_Character_CreatorRepository repCharCre = new AniDB_Character_CreatorRepository();
			List<AniDB_Character_Creator> charCreators = repCharCre.GetByCharID(CharID);

			contract.Creator = null;
			if (charCreators.Count > 0)
			{
				// just use the first creator
				AniDB_CreatorRepository repCreators = new AniDB_CreatorRepository();
				AniDB_Creator creator = repCreators.GetByCreatorID(charCreators[0].CreatorID);
				if (creator != null)
					contract.Creator = creator.ToContract();
			}

			return contract;
		}
	}
}
