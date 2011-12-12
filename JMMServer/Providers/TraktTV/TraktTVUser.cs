using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using JMMContracts;
using JMMServer.Repositories;
using JMMServer.Entities;
using BinaryNorthwest;

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
		public string age { get; set; }

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
			CrossRef_AniDB_TraktRepository repXrefTrakt = new CrossRef_AniDB_TraktRepository();
			CrossRef_AniDB_TvDBRepository repXrefTvDB = new CrossRef_AniDB_TvDBRepository();
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
			Trakt_FriendRepository repFriends = new Trakt_FriendRepository();
			Trakt_EpisodeRepository repEpisodes = new Trakt_EpisodeRepository();

			Trakt_Friend traktFriend = repFriends.GetByUsername(username);
			if (traktFriend == null) return null;

			Contract_Trakt_Friend contract = new Contract_Trakt_Friend();

			contract.Trakt_FriendID = traktFriend.Trakt_FriendID;
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
				if (wtch.episode != null)
				{
					Contract_Trakt_WatchedEpisode watchedEp = new Contract_Trakt_WatchedEpisode();

					watchedEp.Watched = wtch.watched;
					watchedEp.WatchedDate = Utils.GetAniDBDateAsDate(wtch.watched);

					if (!contract.LastEpisodeWatched.HasValue)
						contract.LastEpisodeWatched = watchedEp.WatchedDate;

					if (watchedEp.WatchedDate.HasValue && watchedEp.WatchedDate > contract.LastEpisodeWatched)
						contract.LastEpisodeWatched = watchedEp.WatchedDate;

					watchedEp.AnimeSeriesID = null;

					watchedEp.Episode_Number = wtch.episode.number;
					watchedEp.Episode_Overview = wtch.episode.overview;
					watchedEp.Episode_Season = wtch.episode.season;
					watchedEp.Episode_Title = wtch.episode.title;
					watchedEp.Episode_Url = wtch.episode.url;

					

					if (wtch.episode.images != null)
						watchedEp.Episode_Screenshot = wtch.episode.images.screen;

					if (wtch.show != null)
					{
						watchedEp.TraktShow = wtch.show.ToContract();

						// find the anime and series based on the trakt id
						int? animeID = null;
						CrossRef_AniDB_Trakt xref = repXrefTrakt.GetByTraktID(wtch.show.TraktID, int.Parse(wtch.episode.season));
						if (xref != null)
							animeID = xref.AnimeID;
						else
						{
							// try the tvdb id instead
							CrossRef_AniDB_TvDB xrefTvDB = repXrefTvDB.GetByTvDBID(int.Parse(wtch.show.tvdb_id), int.Parse(wtch.episode.season));
							if (xrefTvDB != null)
								animeID = xrefTvDB.AnimeID;
						}

						if (animeID.HasValue)
						{

							AnimeSeries ser = repSeries.GetByAnimeID(animeID.Value);
							if (ser != null)
								watchedEp.AnimeSeriesID = ser.AnimeSeriesID;

							AniDB_Anime anime = repAnime.GetByAnimeID(animeID.Value);
							if (anime != null)
								watchedEp.Anime = anime.ToContract(true);

						}
					}

					

					contract.WatchedEpisodes.Add(watchedEp);
					break; // only show the latest show
				}

			}

			List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
			sortCriteria.Add(new SortPropOrFieldAndDirection("WatchedDate", true, SortType.eDateTime));
			contract.WatchedEpisodes = Sorting.MultiSort<Contract_Trakt_WatchedEpisode>(contract.WatchedEpisodes, sortCriteria);

			return contract;
		}
	}
}
