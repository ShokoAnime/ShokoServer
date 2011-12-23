using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using NLog;
using System.IO;
using JMMServer.ImageDownload;
using JMMContracts;
using JMMServer.Repositories;
using JMMServer.Providers.TraktTV;

namespace JMMServer.Entities
{
	public class Trakt_Show
	{
		public int Trakt_ShowID { get; private set; }
		public string TraktID { get; set; }
		public string Title { get; set; }
		public string Year { get; set; }
		public string URL { get; set; }
		public string Overview { get; set; }
		public int? TvDB_ID { get; set; }

		public List<Trakt_Season> Seasons
		{
			get
			{
				Trakt_SeasonRepository repSeasons = new Trakt_SeasonRepository();
				return repSeasons.GetByShowID(Trakt_ShowID);
			}
		}

		public void Populate(TraktTVShow tvshow)
		{
			Overview = tvshow.overview;
			Title = tvshow.title;
			TraktID = tvshow.TraktID;
			if (!string.IsNullOrEmpty(tvshow.tvdb_id)) TvDB_ID = int.Parse(tvshow.tvdb_id);
			URL = tvshow.url;
			Year = tvshow.year;
		}

		public void Populate(TraktTV_Show tvshow)
		{
			Overview = tvshow.overview;
			Title = tvshow.title;
			TraktID = tvshow.TraktID;
			if (!string.IsNullOrEmpty(tvshow.tvdb_id)) TvDB_ID = int.Parse(tvshow.tvdb_id);
			URL = tvshow.url;
			Year = tvshow.year;
		}

		public Contract_Trakt_Show ToContract()
		{
			Contract_Trakt_Show contract = new Contract_Trakt_Show();

			contract.Trakt_ShowID = Trakt_ShowID;
			contract.TraktID = TraktID;
			contract.Title = Title;
			contract.Year = Year;
			contract.URL = URL;
			contract.Overview = Overview;
			contract.TvDB_ID = TvDB_ID;
			contract.Seasons = new List<Contract_Trakt_Season>();

			foreach (Trakt_Season season in Seasons)
				contract.Seasons.Add(season.ToContract());

			return contract;
		}
	}
}
