using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI;
using JMMContracts;
using System.IO;
using JMMServer.ImageDownload;

namespace JMMServer.Entities
{
	public class AniDB_Creator
	{
		public int AniDB_CreatorID { get; private set; }
		public int CreatorID { get; set; }
		public string CreatorName { get; set; }
		public string PicName { get; set; }
		public string CreatorKanjiName { get; set; }
		public string CreatorDescription { get; set; }
		public int CreatorType { get; set; }
		public string URLEnglish { get; set; }
		public string URLJapanese { get; set; }
		public string URLWikiEnglish { get; set; }
		public string URLWikiJapanese { get; set; }

		public string PosterPath
		{
			get
			{
				if (string.IsNullOrEmpty(PicName)) return "";

				return Path.Combine(ImageUtils.GetAniDBCreatorImagePath(CreatorID), PicName);
			}
		}

		public void Populate(Raw_AniDB_Creator rawChar)
		{
			this.CreatorDescription = rawChar.CreatorDescription;
			this.CreatorID = rawChar.CreatorID;
			this.CreatorKanjiName = rawChar.CreatorKanjiName;
			this.CreatorName = rawChar.CreatorName;
			this.CreatorType = rawChar.CreatorType;
			this.PicName = rawChar.PicName;
			this.URLEnglish = rawChar.URLEnglish;
			this.URLJapanese = rawChar.URLJapanese;
			this.URLWikiEnglish = rawChar.URLWikiEnglish;
			this.URLWikiJapanese = rawChar.URLWikiJapanese;

		}

		public Contract_AniDB_Creator ToContract()
		{
			Contract_AniDB_Creator contract = new Contract_AniDB_Creator();

			contract.AniDB_CreatorID = this.AniDB_CreatorID;
			contract.CreatorID = this.CreatorID;
			contract.CreatorName = this.CreatorName;
			contract.PicName = this.PicName;
			contract.CreatorKanjiName = this.CreatorKanjiName;
			contract.CreatorDescription = this.CreatorDescription;
			contract.CreatorType = this.CreatorType;
			contract.URLEnglish = this.URLEnglish;
			contract.URLJapanese = this.URLJapanese;
			contract.URLWikiEnglish = this.URLWikiEnglish;
			contract.URLWikiJapanese = this.URLWikiJapanese;

			return contract;
		}
	}
}
