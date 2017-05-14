using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Xml;
using Shoko.Server.Repositories.Direct;
using NHibernate;
using Shoko.Commons.Queue;
using Shoko.Models.Azure;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Commands
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

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct()
                {
                    queueState = QueueStateEnum.SearchTMDb,
                    extraParams = new string[] {AnimeID.ToString()}
                };
            }
        }

        public CommandRequest_MovieDBSearchAnime()
        {
        }

        public CommandRequest_MovieDBSearchAnime(int animeID, bool forced)
        {
            this.AnimeID = animeID;
            this.ForceRefresh = forced;
            this.CommandType = (int) CommandRequestType.MovieDB_SearchAnime;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_MovieDBSearchAnime: {0}", AnimeID);

            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();

                    // first check if the user wants to use the web cache
                    if (ServerSettings.WebCache_TvDB_Get)
                    {
                        try
                        {
                            Azure_CrossRef_AniDB_Other crossRef =
                                AzureWebAPI.Get_CrossRefAniDBOther(AnimeID,
                                    CrossRefType.MovieDB);
                            if (crossRef != null)
                            {
                                int movieID = int.Parse(crossRef.CrossRefID);
                                MovieDB_Movie movie = RepoFactory.MovieDb_Movie.GetByOnlineID(sessionWrapper, movieID);
                                if (movie == null)
                                {
                                    // update the info from online
                                    MovieDBHelper.UpdateMovieInfo(session, movieID, true);
                                    movie = RepoFactory.MovieDb_Movie.GetByOnlineID(movieID);
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

                    // Use TvDB setting
                    if (!ServerSettings.TvDB_AutoLink) return;

                    string searchCriteria = "";
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, AnimeID);
                    if (anime == null) return;

                    searchCriteria = anime.MainTitle;

                    // if not wanting to use web cache, or no match found on the web cache go to TvDB directly
                    List<MovieDB_Movie_Result> results = MovieDBHelper.Search(searchCriteria);
                    logger.Trace("Found {0} moviedb results for {1} on TheTvDB", results.Count, searchCriteria);
                    if (ProcessSearchResults(session, results, searchCriteria)) return;


                    if (results.Count == 0)
                    {
                        foreach (AniDB_Anime_Title title in anime.GetTitles())
                        {
                            if (title.TitleType.ToUpper() != Shoko.Models.Constants.AnimeTitleType.Official.ToUpper())
                                continue;

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
                logger.Trace("Found 1 moviedb results for search on {0} --- Linked to {1} ({2})", searchCriteria,
                    results[0].MovieName, results[0].MovieID);

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
                this.ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_MovieDBSearchAnime", "ForceRefresh"));
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest
            {
                CommandID = this.CommandID,
                CommandType = this.CommandType,
                Priority = this.Priority,
                CommandDetails = this.ToXML(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}