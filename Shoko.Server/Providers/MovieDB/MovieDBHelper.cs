using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Shoko.Models.Server;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.Commands;
using Shoko.Server.Models;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
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

        public static void SaveMovieToDatabase(MovieDB_Movie_Result searchResult, bool saveImages,
            bool isTrakt)
        {

            // save to the DB
            MovieDB_Movie movie;
            using (var upd = Repo.MovieDb_Movie.BeginAddOrUpdate(() => Repo.MovieDb_Movie.GetByMovieID(searchResult.MovieID).FirstOrDefault()))
            {
                upd.Entity.Populate_RA(searchResult);
                // Only save movie info if source is not trakt, this presents adding tv shows as movies
                // Needs better fix later on
                if (!isTrakt)
                    movie = upd.Commit();
                else
                    movie = upd.Entity;
            }

            if (!saveImages) return;

            int numFanartDownloaded = 0;
            int numPostersDownloaded = 0;

            // save data to the DB and determine the number of images we already have
            foreach (MovieDB_Image_Result img in searchResult.Images)
            {
                if (img.ImageType.Equals("poster", StringComparison.InvariantCultureIgnoreCase))
                {
                    MovieDB_Poster poster;
                    using (var upd = Repo.MovieDB_Poster.BeginAddOrUpdate(() => Repo.MovieDB_Poster.GetByOnlineID(img.URL)))
                    {
                        upd.Entity.Populate_RA(img, movie.MovieId);
                        poster = upd.Commit();
                    }
                    if (!string.IsNullOrEmpty(poster.GetFullImagePath()) && File.Exists(poster.GetFullImagePath()))
                        numPostersDownloaded++;
                }
                else
                {
                    // fanart (backdrop)
                    MovieDB_Fanart fanart;
                    using (var upd = Repo.MovieDB_Fanart.BeginAddOrUpdate(() => Repo.MovieDB_Fanart.GetByOnlineID(img.URL)))
                    {
                        upd.Entity.Populate_RA(img, movie.MovieId);
                        fanart = upd.Commit();
                    }
                    if (!string.IsNullOrEmpty(fanart.GetFullImagePath()) && File.Exists(fanart.GetFullImagePath()))
                        numFanartDownloaded++;
                }
            }

            // download the posters
            if (ServerSettings.Instance.MovieDb.AutoPosters || isTrakt)
            {
                foreach (MovieDB_Poster poster in Repo.MovieDB_Poster.GetByMovieID(movie.MovieId))
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
                            Repo.MovieDB_Poster.Delete(poster.MovieDB_PosterID);
                        }
                    }
                }
            }

            // download the fanart
            if (ServerSettings.Instance.MovieDb.AutoFanart || isTrakt)
            {
                foreach (MovieDB_Fanart fanart in Repo.MovieDB_Fanart.GetByMovieID(movie.MovieId))
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
                            Repo.MovieDB_Fanart.Delete(fanart.MovieDB_FanartID);
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
                SearchContainer<SearchMovie> resultsTemp = client.SearchMovie(criteria);

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
                TvShow result = client.GetTvShow(id, TvShowMethods.Images, null);

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



        public static void UpdateMovieInfo(int movieID, bool saveImages)
        {
            try
            {
                TMDbClient client = new TMDbClient(apiKey);
                Movie movie = client.GetMovie(movieID);
                ImagesWithId imgs = client.GetMovieImages(movieID);

                MovieDB_Movie_Result searchResult = new MovieDB_Movie_Result();
                searchResult.Populate(movie, imgs);

                // save to the DB
                SaveMovieToDatabase(searchResult, saveImages, false);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in ParseBanners: " + ex.ToString());
            }
        }

        public static void LinkAniDBMovieDB(int animeID, int movieDBID, bool fromWebCache)
        {
            // check if we have this information locally
            // if not download it now

            MovieDB_Movie movie = Repo.MovieDb_Movie.GetByMovieID(movieDBID).FirstOrDefault();
            if (movie == null)
            {
                // we download the series info here just so that we have the basic info in the
                // database before the queued task runs later
                UpdateMovieInfo(movieDBID, false);
                movie = Repo.MovieDb_Movie.GetByMovieID(movieDBID).FirstOrDefault();
                if (movie == null) return;
            }

            // download and update series info and images
            UpdateMovieInfo(movieDBID, true);
            CrossRef_AniDB_Other xref;
            using (var upd = Repo.CrossRef_AniDB_Other.BeginAddOrUpdate(() => Repo.CrossRef_AniDB_Other.GetByAnimeIDAndType(animeID, CrossRefType.MovieDB)))
            {
                upd.Entity.AnimeID= animeID;
                if (fromWebCache)
                    upd.Entity.CrossRefSource = (int)CrossRefSource.WebCache;
                else
                    upd.Entity.CrossRefSource = (int)CrossRefSource.User;

                upd.Entity.CrossRefType = (int)CrossRefType.MovieDB;
                upd.Entity.CrossRefID = movieDBID.ToString();
                xref = upd.Commit();
            }          
            SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);

            logger.Trace("Changed moviedb association: {0}", animeID);

            CommandRequest_WebCacheSendXRefAniDBOther req =
                new CommandRequest_WebCacheSendXRefAniDBOther(xref.CrossRef_AniDB_OtherID);
            req.Save();
        }

        public static void RemoveLinkAniDBMovieDB(int animeID)
        {
            CrossRef_AniDB_Other xref = Repo.CrossRef_AniDB_Other.GetByAnimeIDAndType(animeID, CrossRefType.MovieDB);
            if (xref == null) return;

            Repo.CrossRef_AniDB_Other.Delete(xref.CrossRef_AniDB_OtherID);

            CommandRequest_WebCacheDeleteXRefAniDBOther req = new CommandRequest_WebCacheDeleteXRefAniDBOther(animeID,
                CrossRefType.MovieDB);
            req.Save();
        }

        public static void ScanForMatches()
        {
            IReadOnlyList<SVR_AnimeSeries> allSeries = Repo.AnimeSeries.GetAll();

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