﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using NLog;
using JMMServer.Repositories;
using JMMServer.Entities;
using JMMServer.Commands;
using System.IO;
using NHibernate;
using TMDbLib.Client;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Search;

namespace JMMServer.Providers.MovieDB
{
	public class MovieDBHelper
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();
		private static UTF8Encoding enc = new UTF8Encoding();
        private static string apiKey = "8192e8032758f0ef4f7caa1ab7b32dd3";

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
			MovieDB_MovieRepository repMovies = new MovieDB_MovieRepository();
			MovieDB_FanartRepository repFanart = new MovieDB_FanartRepository();
			MovieDB_PosterRepository repPosters = new MovieDB_PosterRepository();

			// save to the DB
			MovieDB_Movie movie = repMovies.GetByOnlineID(searchResult.MovieID);
			if (movie == null) movie = new MovieDB_Movie();
			movie.Populate(searchResult);
			repMovies.Save(session, movie);

			if (!saveImages) return;

			int numFanartDownloaded = 0;
			int numPostersDownloaded = 0;

            // save data to the DB and determine the number of images we already have
            foreach (MovieDB_Image_Result img in searchResult.Images)
            {
                if (img.ImageType.Equals("poster", StringComparison.InvariantCultureIgnoreCase))
                {
                    MovieDB_Poster poster = repPosters.GetByOnlineID(session, img.URL);
                    if (poster == null) poster = new MovieDB_Poster();
                    poster.Populate(img, movie.MovieId);
                    repPosters.Save(session, poster);

                    if (!string.IsNullOrEmpty(poster.FullImagePath) && File.Exists(poster.FullImagePath))
                        numPostersDownloaded++;
                }
                else
                {
                    // fanart (backdrop)
                    MovieDB_Fanart fanart = repFanart.GetByOnlineID(session, img.URL);
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
                foreach (MovieDB_Poster poster in repPosters.GetByMovieID(session, movie.MovieId))
                {
                    if (numPostersDownloaded >= ServerSettings.MovieDB_AutoPostersAmount) break;

                    // download the image
                    if (!string.IsNullOrEmpty(poster.FullImagePath) && !File.Exists(poster.FullImagePath))
                    {
                        CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(poster.MovieDB_PosterID, JMMImageType.MovieDB_Poster, false);
                        cmd.Save(session);
                        numPostersDownloaded++;
                    }
                }
            }

            // download the fanart
            if (ServerSettings.MovieDB_AutoFanart)
            {
                foreach (MovieDB_Fanart fanart in repFanart.GetByMovieID(session, movie.MovieId))
                {
                    if (numFanartDownloaded >= ServerSettings.MovieDB_AutoFanartAmount) break;

                    // download the image
                    if (!string.IsNullOrEmpty(fanart.FullImagePath) && !File.Exists(fanart.FullImagePath))
                    {
                        CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(fanart.MovieDB_FanartID, JMMImageType.MovieDB_FanArt, false);
						cmd.Save(session);
						numFanartDownloaded++;
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

                Console.WriteLine("Got {0} of {1} results", resultsTemp.Results.Count, resultsTemp.TotalResults);
                foreach (SearchMovie result in resultsTemp.Results)
                {
                    MovieDB_Movie_Result searchResult = new MovieDB_Movie_Result();
                    Movie movie = client.GetMovie(result.Id);
                    ImagesWithId imgs = client.GetMovieImages(result.Id);
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
                TMDbClient client = new TMDbClient(apiKey);
                Movie movie = client.GetMovie(movieID);
                ImagesWithId imgs = client.GetMovieImages(movieID);

                MovieDB_Movie_Result searchResult = new MovieDB_Movie_Result();
                searchResult.Populate(movie, imgs);

                // save to the DB
                SaveMovieToDatabase(session, searchResult, saveImages);
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in ParseBanners: " + ex.ToString(), ex);
			}
		}

		public static void LinkAniDBMovieDB(int animeID, int movieDBID, bool fromWebCache)
		{
			// check if we have this information locally
			// if not download it now
			MovieDB_MovieRepository repMovies = new MovieDB_MovieRepository();
			MovieDB_Movie movie = repMovies.GetByOnlineID(movieDBID);
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

			CrossRef_AniDB_OtherRepository repCrossRef = new CrossRef_AniDB_OtherRepository();
			CrossRef_AniDB_Other xref = repCrossRef.GetByAnimeIDAndType(animeID, CrossRefType.MovieDB);
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
            AniDB_Anime.UpdateStatsByAnimeID(animeID);

			logger.Trace("Changed moviedb association: {0}", animeID);

			CommandRequest_WebCacheSendXRefAniDBOther req = new CommandRequest_WebCacheSendXRefAniDBOther(xref.CrossRef_AniDB_OtherID);
			req.Save();
		}

		public static void RemoveLinkAniDBMovieDB(int animeID)
		{
			CrossRef_AniDB_OtherRepository repCrossRef = new CrossRef_AniDB_OtherRepository();
			CrossRef_AniDB_Other xref = repCrossRef.GetByAnimeIDAndType(animeID, CrossRefType.MovieDB);
			if (xref == null) return;

			repCrossRef.Delete(xref.CrossRef_AniDB_OtherID);

			CommandRequest_WebCacheDeleteXRefAniDBOther req = new CommandRequest_WebCacheDeleteXRefAniDBOther(animeID, CrossRefType.MovieDB);
			req.Save();
		}

		public static void ScanForMatches()
		{
			AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
			List<AnimeSeries> allSeries = repSeries.GetAll();

			CrossRef_AniDB_OtherRepository repCrossRef = new CrossRef_AniDB_OtherRepository();
			foreach (AnimeSeries ser in allSeries)
			{
				AniDB_Anime anime = ser.GetAnime();
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

				CommandRequest_MovieDBSearchAnime cmd = new CommandRequest_MovieDBSearchAnime(ser.AniDB_ID, false);
				cmd.Save();
			}

		}
	}
}
