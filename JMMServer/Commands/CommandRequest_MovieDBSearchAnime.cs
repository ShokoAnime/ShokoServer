using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Providers.Azure;
using JMMServer.Providers.MovieDB;
using JMMServer.Repositories;
using NHibernate;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_MovieDBSearchAnime : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_MovieDBSearchAnime()
        {
        }

        public CommandRequest_MovieDBSearchAnime(int animeID, bool forced)
        {
            AnimeID = animeID;
            ForceRefresh = forced;
            CommandType = (int)CommandRequestType.MovieDB_SearchAnime;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

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
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_SearchTMDb, AnimeID);
            }
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
                            var repMovies = new MovieDB_MovieRepository();

                            var crossRef = AzureWebAPI.Get_CrossRefAniDBOther(AnimeID, CrossRefType.MovieDB);
                            if (crossRef != null)
                            {
                                var movieID = int.Parse(crossRef.CrossRefID);
                                var movie = repMovies.GetByOnlineID(session, movieID);
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

                    var searchCriteria = "";
                    var repAnime = new AniDB_AnimeRepository();
                    var anime = repAnime.GetByAnimeID(session, AnimeID);
                    if (anime == null) return;

                    searchCriteria = anime.MainTitle;

                    // if not wanting to use web cache, or no match found on the web cache go to TvDB directly
                    var results = MovieDBHelper.Search(searchCriteria);
                    logger.Trace("Found {0} moviedb results for {1} on TheTvDB", results.Count, searchCriteria);
                    if (ProcessSearchResults(session, results, searchCriteria)) return;


                    if (results.Count == 0)
                    {
                        foreach (var title in anime.GetTitles(session))
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
            }
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            CommandType = cq.CommandType;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                var docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_MovieDBSearchAnime", "AnimeID"));
                ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_MovieDBSearchAnime", "ForceRefresh"));
            }

            return true;
        }

        private bool ProcessSearchResults(ISession session, List<MovieDB_Movie_Result> results, string searchCriteria)
        {
            if (results.Count == 1)
            {
                // since we are using this result, lets download the info
                logger.Trace("Found 1 moviedb results for search on {0} --- Linked to {1} ({2})", searchCriteria,
                    results[0].MovieName, results[0].MovieID);

                var movieID = results[0].MovieID;
                MovieDBHelper.UpdateMovieInfo(session, movieID, true);
                MovieDBHelper.LinkAniDBMovieDB(AnimeID, movieID, false);
                return true;
            }

            return false;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_MovieDBSearchAnime{0}", AnimeID);
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            var cq = new CommandRequest();
            cq.CommandID = CommandID;
            cq.CommandType = CommandType;
            cq.Priority = Priority;
            cq.CommandDetails = ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}