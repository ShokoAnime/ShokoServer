using System;
using System.Collections.Generic;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Models.WebCache;
using Shoko.Server.Models;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.WebCache;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.CommandQueue.Commands.MovieDB
{

    public class CmdMovieDBSearchAnime : BaseCommand, ICommand
    {
        public int AnimeID { get; set; }
        public bool ForceRefresh { get; set; }

        
        public string ParallelTag { get; set; } = WorkTypes.MovieDB;
        public int ParallelMax { get; set; } = 4;
        public int Priority { get; set; } = 6;

        public string Id => $"MovieDBSearchAnime_{AnimeID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.SearchTMDb,
            ExtraParams = new[] {AnimeID.ToString()}
        };

        public string WorkType => WorkTypes.MovieDB;

        public CmdMovieDBSearchAnime(string str) : base(str)
        {
        }

        public CmdMovieDBSearchAnime(int animeID, bool forced)
        {
            AnimeID = animeID;
            ForceRefresh = forced;
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_MovieDBSearchAnime: {0}", AnimeID);

            try
            {
                {
                    ReportInit(progress);
                    // first check if the user wants to use the web cache
                    if (ServerSettings.Instance.WebCache.TvDB_Get)
                    {
                        try
                        {
                            WebCache_CrossRef_AniDB_Other crossRef =
                                WebCacheAPI.Get_CrossRefAniDBOther(AnimeID,
                                    CrossRefType.MovieDB);
                            if (crossRef != null)
                            {
                                int movieID = int.Parse(crossRef.CrossRefID);
                                MovieDB_Movie movie = Repo.Instance.MovieDb_Movie.GetByOnlineID(movieID);
                                if (movie == null)
                                {
                                    // update the info from online
                                    MovieDBHelper.UpdateMovieInfo(movieID, true);
                                    movie = Repo.Instance.MovieDb_Movie.GetByOnlineID(movieID);
                                }
                                ReportUpdate(progress, 20);

                                if (movie != null)
                                {
                                    // since we are using the web cache result, let's save it
                                    MovieDBHelper.LinkAniDBMovieDB(AnimeID, movieID, true);
                                    ReportFinish(progress);
                                    return;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            //Ignore
                        }
                    }

                    // Use TvDB setting
                    if (!ServerSettings.Instance.TvDB.AutoLink)
                    {
                        ReportFinish(progress);
                        return;
                    }

                    ReportUpdate(progress, 40);
                    
                    SVR_AniDB_Anime anime = Repo.Instance.AniDB_Anime.GetByAnimeID(AnimeID);
                    if (anime == null)
                    {
                        ReportFinish(progress);
                        return;
                    }

                    string searchCriteria = anime.MainTitle;

                    // if not wanting to use web cache, or no match found on the web cache go to TvDB directly
                    List<MovieDB_Movie_Result> results = MovieDBHelper.Search(searchCriteria);
                    logger.Trace("Found {0} moviedb results for {1} on TheTvDB", results.Count, searchCriteria);
                    ReportUpdate(progress, 60);
                    if (ProcessSearchResults(results, searchCriteria))
                    {
                        ReportFinish(progress);
                        return;
                    }

                    ReportUpdate(progress, 80);

                    if (results.Count == 0)
                    {
                        foreach (AniDB_Anime_Title title in anime.GetTitles())
                        {
                            if (!string.Equals(title.TitleType, Shoko.Models.Constants.AnimeTitleType.Official, StringComparison.OrdinalIgnoreCase))
                                continue;

                            if (string.Equals(searchCriteria, title.Title, StringComparison.OrdinalIgnoreCase)) continue;

                            results = MovieDBHelper.Search(title.Title);
                            logger.Trace("Found {0} moviedb results for search on {1}", results.Count, title.Title);
                            if (ProcessSearchResults(results, title.Title))
                            {
                                ReportFinish(progress);
                                return;
                            }
                        }
                    }

                    ReportFinish(progress);
                }
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing CommandRequest_TvDBSearchAnime: {AnimeID} - {ex}", ex);
            }
        }

        private bool ProcessSearchResults(List<MovieDB_Movie_Result> results, string searchCriteria)
        {
            if (results.Count == 1)
            {
                // since we are using this result, lets download the info
                logger.Trace("Found 1 moviedb results for search on {0} --- Linked to {1} ({2})", searchCriteria,
                    results[0].MovieName, results[0].MovieID);

                int movieID = results[0].MovieID;
                MovieDBHelper.UpdateMovieInfo(movieID, true);
                MovieDBHelper.LinkAniDBMovieDB(AnimeID, movieID, false);
                return true;
            }

            return false;
        }
    }
}