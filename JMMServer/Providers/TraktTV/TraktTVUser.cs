using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using JMMContracts;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTVUser
	{
		public TraktTVUser() { }

		[DataMember]
		public string username { get; set; }
		//public bool protected { get; set; }

		[DataMember]
		public string full_name { get; set; }

		[DataMember]
		public string gender { get; set; }

		[DataMember]
		public object age { get; set; }

		[DataMember]
		public string location { get; set; }

		[DataMember]
		public string about { get; set; }

		[DataMember]
		public int joined { get; set; }

		[DataMember]
		public string avatar { get; set; }

		[DataMember]
		public string url { get; set; }

		[DataMember]
		public object[] watching { get; set; }

		[DataMember]
		public TraktTVWatched[] watched { get; set; }

		public override string ToString()
		{
			return string.Format("{0}", username);
		}

		public Contract_Trakt_Friend ToContract()
		{
			Contract_Trakt_Friend contract = new Contract_Trakt_Friend();

			contract.Username = username;
			contract.Full_name = full_name;
			contract.Gender = gender;
			contract.Age = age;
			contract.Location = location;
			contract.About = about;
			contract.Joined = joined;
			contract.Avatar = avatar;
			contract.Url = url;
			contract.JoinedDate = Utils.GetAniDBDateAsDate(joined);

			contract.WatchedEpisodes = new List<Contract_Trakt_WatchedEpisode>();

			// we only care about the watched episodes
			foreach (TraktTVWatched wtch in watched)
			{
				Contract_Trakt_WatchedEpisode watchedEp = new Contract_Trakt_WatchedEpisode();

				if (wtch.episode != null)
				{
					watchedEp.Watched = wtch.watched;
					watchedEp.WatchedDate = Utils.GetAniDBDateAsDate(wtch.watched);

					watchedEp.Episode_Number = wtch.episode.number;
					watchedEp.Episode_Overview = wtch.episode.overview;
					watchedEp.Episode_Season = wtch.episode.season;
					watchedEp.Episode_Title = wtch.episode.title;
					watchedEp.Episode_Url = wtch.episode.url;

					if (wtch.episode.images != null)
						watchedEp.Episode_Screenshot = wtch.episode.images.screen;
				}

				if (wtch.show != null)
					watchedEp.TraktShow = wtch.show.ToContract();

				contract.WatchedEpisodes.Add(watchedEp);
				
			}

			return contract;
		}
	}
}
