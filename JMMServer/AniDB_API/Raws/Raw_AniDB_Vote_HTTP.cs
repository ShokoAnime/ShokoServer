using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI;
using System.Xml;
using System.Globalization;

namespace JMMServer.AniDB_API.Raws
{
	public class Raw_AniDB_Vote_HTTP : XMLBase
	{
		public int EntityID { get; set; }
		public int VoteValue { get; set; }
		public enAniDBVoteType VoteType { get; set; }



		public Raw_AniDB_Vote_HTTP()
		{
			EntityID = -1;
			VoteValue = -1;
			VoteType = enAniDBVoteType.Anime;
		}


		public void ProcessAnime(XmlNode node)
		{
			NumberStyles style = NumberStyles.Number;
			CultureInfo culture = CultureInfo.CreateSpecificCulture("en-GB");

			this.VoteType = enAniDBVoteType.Anime;
			this.EntityID = int.Parse(node.Attributes["aid"].Value);
			double val = 0;
			double.TryParse(node.InnerText.Trim(), style, culture, out val);
			int ival = 0;
			int.TryParse((val * (double)100).ToString(), out ival);
			VoteValue = ival;
		}

		public void ProcessAnimeTemp(XmlNode node)
		{
			NumberStyles style = NumberStyles.Number;
			CultureInfo culture = CultureInfo.CreateSpecificCulture("en-GB");

			this.VoteType = enAniDBVoteType.AnimeTemp;
			this.EntityID = int.Parse(node.Attributes["aid"].Value);
			double val = 0;
			double.TryParse(node.InnerText.Trim(), style, culture, out val);
			int ival = 0;
			int.TryParse((val * (double)100).ToString(), out ival);
			VoteValue = ival;
		}

		public void ProcessEpisode(XmlNode node)
		{
			NumberStyles style = NumberStyles.Number;
			CultureInfo culture = CultureInfo.CreateSpecificCulture("en-GB");

			this.VoteType = enAniDBVoteType.Episode;
			this.EntityID = int.Parse(node.Attributes["eid"].Value);
			double val = 0;
			double.TryParse(node.InnerText.Trim(), style, culture, out val);
			int ival = 0;
			int.TryParse((val * (double)100).ToString(), out ival);
			VoteValue = ival;
		}


		public override string ToString()
		{
			return string.Format("AniDB_Vote:: entityID: {0} | voteValue: {1} | voteType: {2}",
				EntityID, VoteValue, VoteType);
		}
	}
}
