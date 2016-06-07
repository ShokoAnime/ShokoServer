using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JMMServer.Commands;
using JMMServer.Entities;
using JMMServer.Repositories;
using NHibernate;
using NLog;
using TMDbLib.Client;

namespace JMMServer.Providers.MovieDB
{
    public class MovieDBHelper
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static UTF8Encoding enc = new UTF8Encoding();
        private static readonly string apiKey = "8192e8032758f0ef4f7caa1ab7b32dd3";

        public static string SearchURL
        {
            get { return @"http://api.themoviedb.org/2.1/Movie.search/en/xml/{0}/{1}"; }
        }

        public static string InfoURL
        {
            get { return @"http://api.themoviedb.org/2.1/Movie.getInfo/en/xml/{0}/{1}"; }
        }

        public static void SaveMovieToDatabase(MovieDB_Movie_Result searchResult, bool saveImages)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                SaveMovieToDatabase(session, searchResult, saveImages);
            }
        }

        public static void SaveMovieToDatabase(ISession session, MovieDB_Movie_Result searchResult, bool saveImages)
        {
            var repMovies = new MovieDB_MovieRepository();
            var repFanart = new MovieDB_FanartRepository();
            var repPosters = new MovieDB_PosterRepository();

            // save to the DB
            var movie = repMovies.GetByOnlineID(searchResult.MovieID);
            if (movie == null) movie = new MovieDB_Movie();
            movie.Populate(searchResult);
            repMovies.Save(session, movie);

            if (!saveImages) return;

            var numFanartDownloaded = 0;
            var numPostersDownloaded = 0;

            // save data to the DB and determine the number of images we already have
            foreach (var img in searchResult.Images)
            {
                if (img.ImageType.Equals("poster", StringComparison.InvariantCultureIgnoreCase))
                {
                    var poster = repPosters.GetByOnlineID(session, img.URL);
                    if (poster == null) poster = new MovieDB_Poster();
                    poster.Populate(img, movie.MovieId);
                    repPosters.Save(session, poster);

                    if (!string.IsNullOrEmpty(poster.FullImagePath) && File.Exists(poster.FullImagePath))
                        numPostersDownloaded++;
                }
                else
                {
                    // fanart (backdrop)
                    var fanart = repFanart.GetByOnlineID(session, img.URL);
                    if (fanart == null) fanart = new MovieDB_Fanart();
                    fanart.Populate(img, movie.MovieId);
                    repFanart.Save(session, fanart);

                    if (!string.IsNullOrEmpty(fanart.FullImagePath) && File.Exists(fanart.FullImagePath))
                        numFanartDownloaded++;
                }
            }

            // download the posters
            if (ServerSettings.MovieDB_AutoPosters)
            {
                foreach (var poster in repPosters.GetByMovieID(session, movie.MovieId))
                {
                    if (numPostersDownloaded >= ServerSettings.MovieDB_AutoPostersAmount) break;

                    // download the image
                    if (!string.IsNullOrEmpty(poster.FullImagePath) && !File.Exists(poster.FullImagePath))
                    {
                        var cmd = new CommandRequest_DownloadImage(poster.MovieDB_PosterID, JMMImageType.MovieDB_Poster,
                            false);
                        cmd.Save(session);
                        numPostersDownloaded++;
                    }
                }
            }

            // download the fanart
            if (ServerSettings.MovieDB_AutoFanart)
            {
                foreach (var fanart in repFanart.GetByMovieID(session, movie.MovieId))
                {
                    if (numFanartDownloaded >= ServerSettings.MovieDB_AutoFanartAmount) break;

                    // download the image
                    if (!string.IsNullOrEmpty(fanart.FullImagePath) && !File.Exists(fanart.FullImagePath))
                    {
                        var cmd = new CommandRequest_DownloadImage(fanart.MovieDB_FanartID, JMMImageType.MovieDB_FanArt,
                            false);
                        cmd.Save(session);
                        numFanartDownloaded++;
                    }
                }
            }
        }

        public static List<MovieDB_Movie_Result> Search(string criteria)
        {
            var results = new List<MovieDB_Movie_Result>();

            try
            {
                var client = new TMDbClient(apiKey);
                var resultsTemp = client.SearchMovie(criteria);

                Console.WriteLine("Got {0} of {1} results", resultsTemp.Results.Count, resultsTemp.TotalResults);
                foreach (var result in resultsTemp.Results)
                {
                    var searchResult = new MovieDB_Movie_Result();
                    var movie = client.GetMovie(result.Id);
                    var imgs = client.GetMovieImages(result.Id);
                    searchResult.Populate(movie, imgs);
                    results.Add(searchResult);
                    SaveMovieToDatabase(searchResult, false);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error in MovieDB Search: " + ex.Message);
            }

            return results;
        }

        public static void UpdateMovieInfo(int movieID, bool saveImages)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                UpdateMovieInfo(session, movieID, saveImages);
            }
        }

        public static void UpdateMovieInfo(ISession session, int movieID, bool saveImages)
        {
            try
            {
                var client = new TMDbClient(apiKey);
                var movie = client.GetMovie(movieID);
                var imgs = client.GetMovieImages(movieID);

                var searchResult = new MovieDB_Movie_Result();
                searchResult.Populate(movie, imgs);

                // save to the DB
                SaveMovieToDatabase(session, searchResult, saveImages);
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in ParseBanners: " + ex, ex);
            }
        }

        public static void LinkAniDBMovieDB(int animeID, int movieDBID, bool fromWebCache)
        {
            // check if we have this information locally
            // if not download it now
            var repMovies = new MovieDB_MovieRepository();
            var movie = repMovies.GetByOnlineID(movieDBID);
            if (movie == null)
            {
                // we download the series info here just so that we have the basic info in the
                // database before the queued task runs later
                UpdateMovieInfo(movieDBID, false);
                movie = repMovies.GetByOnlineID(movieDBID);
                if (movie == null) return;
            }

            // download and update series info and images
            UpdateMovieInfo(movieDBID, true);

            var repCrossRef = new CrossRef_AniDB_OtherRepository();
            var xref = repCrossRef.GetByAnimeIDAndType(animeID, CrossRefType.MovieDB);
            if (xref == null)
                xref = new CrossRef_AniDB_Other();

            xref.AnimeID = animeID;
            if (fromWebCache)
                xref.CrossRefSource = (int)CrossRefSource.WebCache;
            else
                xref.CrossRefSource = (int)CrossRefSource.User;

            xref.CrossRefType = (int)CrossRefType.MovieDB;
            xref.CrossRefID = movieDBID.ToString();
            repCrossRef.Save(xref);

            StatsCache.Instance.UpdateUsingAnime(animeID);

            logger.Trace("Changed moviedb association: {0}", animeID);

            var req = new CommandRequest_WebCacheSendXRefAniDBOther(xref.CrossRef_AniDB_OtherID);
            req.Save();
        }

        public static void RemoveLinkAniDBMovieDB(int animeID)
        {
            var repCrossRef = new CrossRef_AniDB_OtherRepository();
            var xref = repCrossRef.GetByAnimeIDAndType(animeID, CrossRefType.MovieDB);
            if (xref == null) return;

            repCrossRef.Delete(xref.CrossRef_AniDB_OtherID);

            var req = new CommandRequest_WebCacheDeleteXRefAniDBOther(animeID, CrossRefType.MovieDB);
            req.Save();
        }

        public static void ScanForMatches()
        {
            var repSeries = new AnimeSeriesRepository();
            var allSeries = repSeries.GetAll();

            var repCrossRef = new CrossRef_AniDB_OtherRepository();
            foreach (var ser in allSeries)
            {
                var anime = ser.GetAnime();
                if (anime == null) continue;

                if (anime.IsMovieDBLinkDisabled) continue;

                // don't scan if it is associated on the TvDB
                if (anime.GetCrossRefTvDBV2().Count > 0) continue;

                // don't scan if it is associated on the MovieDB
                if (anime.GetCrossRefMovieDB() != null) continue;

                // don't scan if it is not a movie
                if (!anime.SearchOnMovieDB)
                    continue;

                logger.Trace("Found anime movie without MovieDB association: " + anime.MainTitle);

                var cmd = new CommandRequest_MovieDBSearchAnime(ser.AniDB_ID, false);
                cmd.Save();
            }
        }
    }
}