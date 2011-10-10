using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace AniDBAPI
{
	[Serializable]
	public class Raw_AniDB_SimilarAnime : XMLBase
	{
		public int AnimeID { get; set; }
		public int SimilarAnimeID { get; set; }
		public int Approval { get; set; }
		public int Total { get; set; }

		public Raw_AniDB_SimilarAnime()
		{
			InitFields();
		}

		private void InitFields()
		{
			AnimeID = 0;
			SimilarAnimeID = 0;
			Approval = 0;
			Total = 0;
		}

		public void ProcessFromHTTPResult(XmlNode node, int anid)
		{
			InitFields();

			this.AnimeID = anid;

			int id = 0;
			int.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "id"), out id);
			this.SimilarAnimeID = id;

			int appr = 0;
			int.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "approval"), out appr);
			this.Approval = appr;

			int tot = 0;
			int.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "total"), out tot);
			this.Total = tot;
		}
	}
}
