using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using Microsoft.Extensions.Logging;
using NHibernate;
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

namespace Shoko.Server.Providers.MovieDB;

public class MovieDBHelper
{
    private readonly ILogger<MovieDBHelper> _logger;
    private readonly ICommandRequestFactory _commandFactory;
    private const string APIKey = "8192e8032758f0ef4f7caa1ab7b32dd3";

    public MovieDBHelper(ILogger<MovieDBHelper> logger, ICommandRequestFactory commandFactory)
    {
        _logger = logger;
        _commandFactory = commandFactory;
    }

    private void SaveMovieToDatabase(MovieDB_Movie_Result searchResult, bool saveImages, bool isTrakt)
    {
        // save to the DB
        var movie = RepoFactory.MovieDb_Movie.GetByOnlineID(searchResult.MovieID) ?? new MovieDB_Movie();
        movie.Populate(searchResult);

        // Only save movie info if source is not trakt, this presents adding tv shows as movies
        // Needs better fix later on

        if (!isTrakt)
        {
            RepoFactory.MovieDb_Movie.Save(movie);
        }

        if (!saveImages)
        {
            return;
        }

        var numFanartDownloaded = 0;
        var numPostersDownloaded = 0;

        // save data to the DB and determine the number of images we already have
        foreach (var img in searchResult.Images)
        {
            if (img.ImageType.Equals("poster", StringComparison.InvariantCultureIgnoreCase))
            {
                var poster = RepoFactory.MovieDB_Poster.GetByOnlineID(img.URL) ?? new MovieDB_Poster();
                poster.Populate(img, movie.MovieId);
                RepoFactory.MovieDB_Poster.Save(poster);

                if (!string.IsNullOrEmpty(poster.GetFullImagePath()) && File.Exists(poster.GetFullImagePath()))
                {
                    numPostersDownloaded++;
                }
            }
            else
            {
                // fanart (backdrop)
                var fanart = RepoFactory.MovieDB_Fanart.GetByOnlineID(img.URL) ?? new MovieDB_Fanart();
                fanart.Populate(img, movie.MovieId);
                RepoFactory.MovieDB_Fanart.Save(fanart);

                if (!string.IsNullOrEmpty(fanart.GetFullImagePath()) && File.Exists(fanart.GetFullImagePath()))
                {
                    numFanartDownloaded++;
                }
            }
        }

        // download the posters
        if (ServerSettings.Instance.MovieDb.AutoPosters || isTrakt)
        {
            foreach (var poster in RepoFactory.MovieDB_Poster.GetByMovieID( movie.MovieId))
            {
                if (numPostersDownloaded < ServerSettings.Instance.MovieDb.AutoPostersAmount)
                {
                    // download the image
                    if (string.IsNullOrEmpty(poster.GetFullImagePath()) || File.Exists(poster.GetFullImagePath()))
                    {
                        continue;
                    }

                    var cmd = _commandFactory.Create<CommandRequest_DownloadImage>(
                        c =>
                        {
                            c.EntityID = poster.MovieDB_PosterID;
                            c.EntityType = (int)ImageEntityType.MovieDB_Poster;
                        }
                    );
                    cmd.Save();
                    numPostersDownloaded++;
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
            foreach (var fanart in RepoFactory.MovieDB_Fanart.GetByMovieID(movie.MovieId))
            {
                if (numFanartDownloaded < ServerSettings.Instance.MovieDb.AutoFanartAmount)
                {
                    // download the image
                    if (string.IsNullOrEmpty(fanart.GetFullImagePath()) || File.Exists(fanart.GetFullImagePath()))
                    {
                        continue;
                    }

                    var cmd = _commandFactory.Create<CommandRequest_DownloadImage>(
                        c =>
                        {
                            c.EntityID = fanart.MovieDB_FanartID;
                            c.EntityType = (int)ImageEntityType.MovieDB_FanArt;
                        }
                    );
                    cmd.Save();
                    numFanartDownloaded++;
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

    public List<MovieDB_Movie_Result> Search(string criteria)
    {
        var results = new List<MovieDB_Movie_Result>();

        try
        {
            var client = new TMDbClient(APIKey);
            var resultsTemp = client.SearchMovie(HttpUtility.UrlDecode(criteria));

            _logger.LogInformation("Got {Count} of {Results} results", resultsTemp.Results.Count,
                resultsTemp.TotalResults);
            foreach (var result in resultsTemp.Results)
            {
                var searchResult = new MovieDB_Movie_Result();
                var movie = client.GetMovie(result.Id);
                var imgs = client.GetMovieImages(result.Id);
                searchResult.Populate(movie, imgs);
                results.Add(searchResult);
                SaveMovieToDatabase(searchResult, false, false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error in MovieDB Search: {Message}", ex.Message);
        }

        return results;
    }

    public void UpdateAllMovieInfo(bool saveImages)
    {
        using var session = DatabaseFactory.SessionFactory.OpenSession();
        var all = RepoFactory.MovieDb_Movie.GetAll();
        var max = all.Count;
        var i = 0;
        foreach (var movie in all)
        {
            try
            {
                i++;
                _logger.LogInformation("Updating MovieDB Movie {I}/{Max}", i, max);
                UpdateMovieInfo(movie.MovieId, saveImages);
            }
            catch (Exception e)
            {
                _logger.LogError("Failed to Update MovieDB Movie ID: {Id} Error: {E}", movie.MovieId, e);
            }
        }
    }

    public void UpdateMovieInfo(int movieID, bool saveImages)
    {
        try
        {
            var client = new TMDbClient(APIKey);
            var movie = client.GetMovie(movieID);
            var imgs = client.GetMovieImages(movieID);

            var searchResult = new MovieDB_Movie_Result();
            searchResult.Populate(movie, imgs);

            // save to the DB
            SaveMovieToDatabase(searchResult, saveImages, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UpdateMovieInfo: {Ex}", ex);
        }
    }

    public void LinkAniDBMovieDB(int animeID, int movieDBID, bool fromWebCache)
    {
        // check if we have this information locally
        // if not download it now
        var movie = RepoFactory.MovieDb_Movie.GetByOnlineID(movieDBID);
        if (movie == null)
        {
            // we download the series info here just so that we have the basic info in the
            // database before the queued task runs later
            UpdateMovieInfo(movieDBID, false);
            movie = RepoFactory.MovieDb_Movie.GetByOnlineID(movieDBID);
            if (movie == null)
            {
                return;
            }
        }

        // download and update series info and images
        UpdateMovieInfo(movieDBID, true);

        var xref = RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(animeID, CrossRefType.MovieDB) ??
                   new CrossRef_AniDB_Other();

        xref.AnimeID = animeID;
        if (fromWebCache)
        {
            xref.CrossRefSource = (int)CrossRefSource.WebCache;
        }
        else
        {
            xref.CrossRefSource = (int)CrossRefSource.User;
        }

        xref.CrossRefType = (int)CrossRefType.MovieDB;
        xref.CrossRefID = movieDBID.ToString();
        RepoFactory.CrossRef_AniDB_Other.Save(xref);
        SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);

        _logger.LogTrace("Changed moviedb association: {AnimeID}", animeID);
    }

    public void RemoveLinkAniDBMovieDB(int animeID)
    {
        var xref =
            RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(animeID, CrossRefType.MovieDB);
        if (xref == null)
        {
            return;
        }

        RepoFactory.CrossRef_AniDB_Other.Delete(xref.CrossRef_AniDB_OtherID);
    }

    public void ScanForMatches()
    {
        var allSeries = RepoFactory.AnimeSeries.GetAll();

        foreach (var ser in allSeries)
        {
            var anime = ser.GetAnime();
            if (anime == null)
            {
                continue;
            }

            if (anime.IsMovieDBLinkDisabled())
            {
                continue;
            }

            // don't scan if it is associated on the TvDB
            if (anime.GetCrossRefTvDB().Count > 0)
            {
                continue;
            }

            // don't scan if it is associated on the MovieDB
            if (anime.GetCrossRefMovieDB() != null)
            {
                continue;
            }

            // don't scan if it is not a movie
            if (!anime.GetSearchOnMovieDB())
            {
                continue;
            }

            _logger.LogTrace("Found anime movie without MovieDB association: {MainTitle}", anime.MainTitle);

            var cmd = _commandFactory.Create<CommandRequest_MovieDBSearchAnime>(c => c.AnimeID = ser.AniDB_ID);
            cmd.Save();
        }
    }
}
