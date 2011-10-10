using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using AniDBAPI;
using JMMServer.Repositories;

namespace JMMServer.Entities
{
	public class AniDB_Episode
	{
		#region DB columns
		public int AniDB_EpisodeID { get; private set; }
		public int EpisodeID { get; set; }
		public int AnimeID { get; set; }
		public int LengthSeconds { get; set; }
		public string Rating { get; set; }
		public string Votes { get; set; }
		public int EpisodeNumber { get; set; }
		public int EpisodeType { get; set; }
		public string RomajiName { get; set; }
		public string EnglishName { get; set; }
		public int AirDate { get; set; }
		public DateTime DateTimeUpdated { get; set; }
		#endregion

		[XmlIgnore]
		public string AirDateFormatted
		{
			get
			{
				try
				{
					return Utils.GetAniDBDate(AirDate);
				}
				catch (Exception)
				{

					return "";
				}

			}
		}

		[XmlIgnore]
		public DateTime? AirDateAsDate
		{
			get
			{
				return Utils.GetAniDBDateAsDate(AirDate);
			}
		}

		public enEpisodeType EpisodeTypeEnum
		{
			get
			{
				return (enEpisodeType)EpisodeType;
			}
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("episodeID: " + EpisodeID.ToString());
			sb.Append(" | animeID: " + AnimeID.ToString());
			sb.Append(" | episodeNumber: " + EpisodeNumber.ToString());
			sb.Append(" | episodeType: " + EpisodeType);
			sb.Append(" | englishName: " + EnglishName);
			sb.Append(" | airDate: " + AirDate);
			//sb.Append(" | AirDateFormatted: " + AirDateFormatted);

			return sb.ToString();
		}

		public void Populate(Raw_AniDB_Episode epInfo)
		{
			this.AirDate = epInfo.AirDate;
			this.AnimeID = epInfo.AnimeID;
			this.DateTimeUpdated = DateTime.Now;
			this.EnglishName = epInfo.EnglishName;
			this.EpisodeID = epInfo.EpisodeID;
			this.EpisodeNumber = epInfo.EpisodeNumber;
			this.EpisodeType = epInfo.EpisodeType;
			this.LengthSeconds = epInfo.LengthSeconds;
			this.Rating = epInfo.Rating.ToString();
			this.RomajiName = epInfo.RomajiName;
			this.Votes = epInfo.Votes.ToString();
		}


		public void CreateAnimeEpisode(int animeSeriesID)
		{
			// check if there is an existing episode for this EpisodeID
			AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
			List<AnimeEpisode> existingEps = repEps.GetByAniEpisodeIDAndSeriesID(EpisodeID, animeSeriesID);

			if (existingEps.Count == 0)
			{
				AnimeEpisode animeEp = new AnimeEpisode();
				animeEp.Populate(this);
				animeEp.AnimeSeriesID = animeSeriesID;
				repEps.Save(animeEp);
			}
		}
	}
}
