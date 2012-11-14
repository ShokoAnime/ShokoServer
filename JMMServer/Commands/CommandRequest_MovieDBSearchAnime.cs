using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using JMMServer.Repositories;
using JMMServer.Providers.TvDB;
using JMMServer.WebCache;
using System.Xml;
using JMMServer.Providers.MovieDB;
using NHibernate;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_MovieDBSearchAnime : CommandRequestImplementation, ICommandRequest
	{
		public int AnimeID { get; set; }
		public bool ForceRefresh { get; set; }

		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority8; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Searching for anime on The MovieDB: {0}", AnimeID);
			}
		}

		public CommandRequest_MovieDBSearchAnime()
		{
		}

		public CommandRequest_MovieDBSearchAnime(int animeID, bool forced)
		{
			this.AnimeID = animeID;
			this.ForceRefresh = forced;
			this.CommandType = (int)CommandRequestType.MovieDB_SearchAnime;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_MovieDBSearchAnime: {0}", AnimeID);

			try
			{
				using (var session = JMMService.SessionFactory.OpenSession())
				{
					// first check if the user wants to use the web cache
					if (ServerSettings.WebCache_TvDB_Get)
					{
						try
						{
							MovieDB_MovieRepository repMovies = new MovieDB_MovieRepository();

							CrossRef_AniDB_OtherResult crossRef = XMLService.Get_CrossRef_AniDB_Other(AnimeID, CrossRefType.MovieDB);
							if (crossRef != null)
							{
								int movieID = int.Parse(crossRef.CrossRefID);
								MovieDB_Movie movie = repMovies.GetByOnlineID(session, movieID);
								if (movie == null)
								{
									// update the info from online
									MovieDBHelper.UpdateMovieInfo(session, movieID, true);
									movie = repMovies.GetByOnlineID(movieID);
								}

								if (movie != null)
								{
									// since we are using the web cache result, let's save it
									MovieDBHelper.LinkAniDBMovieDB(AnimeID, movieID, true);
									return;
								}

							}
						}
						catch (Exception)
						{
						}
					}

					string searchCriteria = "";
					AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
					AniDB_Anime anime = repAnime.GetByAnimeID(session, AnimeID);
					if (anime == null) return;

					searchCriteria = anime.MainTitle;

					// if not wanting to use web cache, or no match found on the web cache go to TvDB directly
					List<MovieDB_Movie_Result> results = MovieDBHelper.Search(searchCriteria);
					logger.Trace("Found {0} moviedb results for {1} on TheTvDB", results.Count, searchCriteria);
					if (ProcessSearchResults(session, results, searchCriteria)) return;


					if (results.Count == 0)
					{
						foreach (AniDB_Anime_Title title in anime.GetTitles(session))
						{
							if (title.TitleType.ToUpper() != Constants.AnimeTitleType.Official.ToUpper()) continue;

							if (searchCriteria.ToUpper() == title.Title.ToUpper()) continue;

							results = MovieDBHelper.Search(title.Title);
							logger.Trace("Found {0} moviedb results for search on {1}", results.Count, title.Title);
							if (ProcessSearchResults(session, results, title.Title)) return;
						}
					}
				}

			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_TvDBSearchAnime: {0} - {1}", AnimeID, ex.ToString());
				return;
			}
		}

		private bool ProcessSearchResults(ISession session, List<MovieDB_Movie_Result> results, string searchCriteria)
		{
			if (results.Count == 1)
			{
				// since we are using this result, lets download the info
				logger.Trace("Found 1 moviedb results for search on {0} --- Linked to {1} ({2})", searchCriteria, results[0].MovieName, results[0].MovieID);

				int movieID = results[0].MovieID;
				MovieDBHelper.UpdateMovieInfo(session, movieID, true);
				MovieDBHelper.LinkAniDBMovieDB(AnimeID, movieID, false);
				return true;
			}

			return false;
		}

		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_MovieDBSearchAnime{0}", this.AnimeID);
		}

		public override bool LoadFromDBCommand(CommandRequest cq)
		{
			this.CommandID = cq.CommandID;
			this.CommandRequestID = cq.CommandRequestID;
			this.CommandType = cq.CommandType;
			this.Priority = cq.Priority;
			this.CommandDetails = cq.CommandDetails;
			this.DateTimeUpdated = cq.DateTimeUpdated;

			// read xml to get parameters
			if (this.CommandDetails.Trim().Length > 0)
			{
				XmlDocument docCreator = new XmlDocument();
				docCreator.LoadXml(this.CommandDetails);

				// populate the fields
				this.AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_MovieDBSearchAnime", "AnimeID"));
				this.ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_MovieDBSearchAnime", "ForceRefresh"));
			}

			return true;
		}

		public override CommandRequest ToDatabaseObject()
		{
			GenerateCommandID();

			CommandRequest cq = new CommandRequest();
			cq.CommandID = this.CommandID;
			cq.CommandType = this.CommandType;
			cq.Priority = this.Priority;
			cq.CommandDetails = this.ToXML();
			cq.DateTimeUpdated = DateTime.Now;

			return cq;
		}
	}
}
