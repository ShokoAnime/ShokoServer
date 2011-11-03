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
	public class AniDB_Seiyuu
	{
		public int AniDB_SeiyuuID { get; private set; }
		public int SeiyuuID { get; set; }
		public string SeiyuuName { get; set; }
		public string PicName { get; set; }

		public string PosterPath
		{
			get
			{
				if (string.IsNullOrEmpty(PicName)) return "";

				return Path.Combine(ImageUtils.GetAniDBCreatorImagePath(SeiyuuID), PicName);
			}
		}

		public Contract_AniDB_Seiyuu ToContract()
		{
			Contract_AniDB_Seiyuu contract = new Contract_AniDB_Seiyuu();

			contract.AniDB_SeiyuuID = this.AniDB_SeiyuuID;
			contract.SeiyuuID = this.SeiyuuID;
			contract.SeiyuuName = this.SeiyuuName;
			contract.PicName = this.PicName;

			return contract;
		}
	}
}
