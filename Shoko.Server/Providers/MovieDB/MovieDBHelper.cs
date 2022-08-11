using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using NHibernate;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Settings;
using TMDbLib.Client;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;

namespace Shoko.Server.Providers.MovieDB
{
    public class MovieDBHelper
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static string apiKey = "8192e8032758f0ef4f7caa1ab7b32dd3";

        public static void SaveMovieToDatabase(MovieDB_Movie_Result searchResult, bool saveImages, bool isTrakt)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                SaveMovieToDatabase(session, searchResult, saveImages, isTrakt);
            }
        }

        public static void SaveMovieToDatabase(ISession session, MovieDB_Movie_Result searchResult, bool saveImages,
            bool isTrakt)
        {
            ISessionWrapper sessionWrapper = session.Wrap();

            // save to the DB
            MovieDB_Movie movie = RepoFactory.MovieDb_Movie.GetByOnlineID(searchResult.MovieID);
            if (movie == null) movie = new MovieDB_Movie();
            movie.Populate(searchResult);

            // Only save movie info if source is not trakt, this presents adding tv shows as movies
            // Needs better fix later on

            if (!isTrakt)
            {
                RepoFactory.MovieDb_Movie.Save(movie);
            }

            if (!saveImages) return;

            int numFanartDownloaded = 0;
            int numPostersDownloaded = 0;

            // save data to the DB and determine the number of images we already have
            foreach (MovieDB_Image_Result img in searchResult.Images)
            {
                if (img.ImageType.Equals("poster", StringComparison.InvariantCultureIgnoreCase))
                {
                    MovieDB_Poster poster = RepoFactory.MovieDB_Poster.GetByOnlineID(session, img.URL);
                    if (poster == null) poster = new MovieDB_Poster();
                    poster.Populate(img, movie.MovieId);
                    RepoFactory.MovieDB_Poster.Save(poster);

                    if (!string.IsNullOrEmpty(poster.GetFullImagePath()) && File.Exists(poster.GetFullImagePath()))
                        numPostersDownloaded++;
                }
                else
                {
                    // fanart (backdrop)
                    MovieDB_Fanart fanart = RepoFactory.MovieDB_Fanart.GetByOnlineID(session, img.URL);
                    if (fanart == null) fanart = new MovieDB_Fanart();
                    fanart.Populate(img, movie.MovieId);
                    RepoFactory.MovieDB_Fanart.Save(fanart);

                    if (!string.IsNullOrEmpty(fanart.GetFullImagePath()) && File.Exists(fanart.GetFullImagePath()))
                        numFanartDownloaded++;
                }
            }

            // download the posters
            if (ServerSettings.Instance.MovieDb.AutoPosters || isTrakt)
            {
                foreach (MovieDB_Poster poster in RepoFactory.MovieDB_Poster.GetByMovieID(sessionWrapper, movie.MovieId)
                )
                {
                    if (numPostersDownloaded < ServerSettings.Instance.MovieDb.AutoPostersAmount)
                    {
                        // download the image
                        if (!string.IsNullOrEmpty(poster.GetFullImagePath()) && !File.Exists(poster.GetFullImagePath()))
                        {
                            CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(poster.MovieDB_PosterID,
                                ImageEntityType.MovieDB_Poster, false);
                            cmd.Save();
                            numPostersDownloaded++;
                        }
                    }
                    else
                    {
                        //The MovieDB_AutoPostersAmount should prevent from saving image info without image
                        // we should clean those image that we didn't download because those dont exists in local repo
                        // first we check if file was downloaded
                        if (!File.Exists(poster.GetFullImagePath()))
                        {
                            RepoFactory.MovieDB_Poster.Delete(poster.MovieDB_PosterID);
                        }
                    }
                }
            }

            // download the fanart
            if (ServerSettings.Instance.MovieDb.AutoFanart || isTrakt)
            {
                foreach (MovieDB_Fanart fanart in RepoFactory.MovieDB_Fanart.GetByMovieID(sessionWrapper, movie.MovieId)
                )
                {
                    if (numFanartDownloaded < ServerSettings.Instance.MovieDb.AutoFanartAmount)
                    {
                        // download the image
                        if (!string.IsNullOrEmpty(fanart.GetFullImagePath()) && !File.Exists(fanart.GetFullImagePath()))
                        {
                            CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(fanart.MovieDB_FanartID,
                                ImageEntityType.MovieDB_FanArt, false);
                            cmd.Save();
                            numFanartDownloaded++;
                        }
                    }
                    else
                    {
                        //The MovieDB_AutoFanartAmount should prevent from saving image info without image
                        // we should clean those image that we didn't download because those dont exists in local repo
                        // first we check if file was downloaded
                        if (!File.Exists(fanart.GetFullImagePath()))
                        {
                            RepoFactory.MovieDB_Fanart.Delete(fanart.MovieDB_FanartID);
                        }
                    }
                }
            }
        }

        public static List<MovieDB_Movie_Result> Search(string criteria)
        {
            List<MovieDB_Movie_Result> results = new List<MovieDB_Movie_Result>();

            try
            {
                TMDbClient client = new TMDbClient(apiKey);
                SearchContainer<SearchMovie> resultsTemp = client.SearchMovie(HttpUtility.UrlDecode(criteria));

                logger.Info($"Got {resultsTemp.Results.Count} of {resultsTemp.TotalResults} results");
                foreach (SearchMovie result in resultsTemp.Results)
                {
                    MovieDB_Movie_Result searchResult = new MovieDB_Movie_Result();
                    Movie movie = client.GetMovie(result.Id);
                    ImagesWithId imgs = client.GetMovieImages(result.Id);
                    searchResult.Populate(movie, imgs);
                    results.Add(searchResult);
                    SaveMovieToDatabase(searchResult, false, false);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error in MovieDB Search: " + ex.Message);
            }

            return results;
        }

        public static List<MovieDB_Movie_Result> SearchWithTVShowID(int id, bool isTrakt)
        {
            List<MovieDB_Movie_Result> results = new List<MovieDB_Movie_Result>();

            try
            {
                TMDbClient client = new TMDbClient(apiKey);
                TvShow result = client.GetTvShow(id, TvShowMethods.Images);

                if (result != null)
                {
                    logger.Info("Got TMDB results for id: {0} | show name: {1}", id, result.Name);
                    MovieDB_Movie_Result searchResult = new MovieDB_Movie_Result();
                    Movie movie = client.GetMovie(result.Id);
                    ImagesWithId imgs = client.GetMovieImages(result.Id);
                    searchResult.Populate(movie, imgs);
                    results.Add(searchResult);
                    SaveMovieToDatabase(searchResult, true, isTrakt);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error in MovieDB Search: " + ex.Message);
            }

            return results;
        }

        public static void UpdateAllMovieInfo(bool saveImages)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var all = RepoFactory.MovieDb_Movie.GetAll();
                int max = all.Count;
                int i = 0;
                foreach (var batch in all.Batch(50))
                {
                    using (var trans = session.BeginTransaction())
                    {
                        foreach (MovieDB_Movie movie in batch)
                        {
                            try
                            {
                                i++;
                                logger.Info($"Updating MovieDB Movie {i}/{max}");
                                UpdateMovieInfo(session, movie.MovieId, saveImages);
                            }
                            catch (Exception e)
                            {
                                logger.Error($"Failed to Update MovieDB Movie ID: {movie.MovieId} Error: {e}");
                            }
                        }
                        trans.Commit();
                    }
                }
            }
        }

        public static void UpdateMovieInfo(int movieID, bool saveImages)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                UpdateMovieInfo(session, movieID, saveImages);
            }
        }

        public static void UpdateMovieInfo(ISession session, int movieID, bool saveImages)
        {
            try
            {
                TMDbClient client = new TMDbClient(apiKey);
                Movie movie = client.GetMovie(movieID);
                ImagesWithId imgs = client.GetMovieImages(movieID);

                MovieDB_Movie_Result searchResult = new MovieDB_Movie_Result();
                searchResult.Populate(movie, imgs);

                // save to the DB
                SaveMovieToDatabase(session, searchResult, saveImages, false);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in UpdateMovieInfo: " + ex);
            }
        }

        public static void LinkAniDBMovieDB(int animeID, int movieDBID, bool fromWebCache)
        {
            // check if we have this information locally
            // if not download it now

            MovieDB_Movie movie = RepoFactory.MovieDb_Movie.GetByOnlineID(movieDBID);
            if (movie == null)
            {
                // we download the series info here just so that we have the basic info in the
                // database before the queued task runs later
                UpdateMovieInfo(movieDBID, false);
                movie = RepoFactory.MovieDb_Movie.GetByOnlineID(movieDBID);
                if (movie == null) return;
            }

            // download and update series info and images
            UpdateMovieInfo(movieDBID, true);

            CrossRef_AniDB_Other xref =
                RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(animeID, CrossRefType.MovieDB);
            if (xref == null)
                xref = new CrossRef_AniDB_Other();

            xref.AnimeID = animeID;
            if (fromWebCache)
                xref.CrossRefSource = (int) CrossRefSource.WebCache;
            else
                xref.CrossRefSource = (int) CrossRefSource.User;

            xref.CrossRefType = (int) CrossRefType.MovieDB;
            xref.CrossRefID = movieDBID.ToString();
            RepoFactory.CrossRef_AniDB_Other.Save(xref);
            SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);

            logger.Trace("Changed moviedb association: {0}", animeID);
        }

        public static void RemoveLinkAniDBMovieDB(int animeID)
        {
            CrossRef_AniDB_Other xref =
                RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(animeID, CrossRefType.MovieDB);
            if (xref == null) return;

            RepoFactory.CrossRef_AniDB_Other.Delete(xref.CrossRef_AniDB_OtherID);
        }

        public static void ScanForMatches()
        {
            IReadOnlyList<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll();

            foreach (SVR_AnimeSeries ser in allSeries)
            {
                SVR_AniDB_Anime anime = ser.GetAnime();
                if (anime == null) continue;

                if (anime.IsMovieDBLinkDisabled()) continue;

                // don't scan if it is associated on the TvDB
                if (anime.GetCrossRefTvDB().Count > 0) continue;

                // don't scan if it is associated on the MovieDB
                if (anime.GetCrossRefMovieDB() != null) continue;

                // don't scan if it is not a movie
                if (!anime.GetSearchOnMovieDB())
                    continue;

                logger.Trace("Found anime movie without MovieDB association: " + anime.MainTitle);

                CommandRequest_MovieDBSearchAnime cmd = new CommandRequest_MovieDBSearchAnime(ser.AniDB_ID, false);
                cmd.Save();
            }
        }
    }
}