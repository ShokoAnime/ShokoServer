using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Providers.TMDB.Search;

public class TMDBOfflineSearch
{
    // ensure that it doesn't try to download at the same time
    private static readonly object s_accessLock = new();

    private readonly ILogger<TMDBOfflineSearch> _logger;

    public TMDBOfflineSearch(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<TMDBOfflineSearch>();
    }

    #region Movie

    private static string CachedMovieFilePath => Path.Combine(Utils.ApplicationPath, "tmdb-movies.json.gz");

    private IDictionary<int, TMDBOfflineSearch_Movie>? _movies = null;

    public IEnumerable<TMDBOfflineSearch_Movie> GetAllMovies()
    {
        try
        {
            EnsureMoviesAreUsable();
            if (_movies != null)
                return _movies.Values;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all offline TMDB movies; {ex}", ex.Message);
        }
        return Array.Empty<TMDBOfflineSearch_Movie>();
    }

    public TMDBOfflineSearch_Movie? FindMovieByID(int movieId)
    {
        try
        {
            EnsureMoviesAreUsable();
            if (_movies != null && _movies.TryGetValue(movieId, out var movie))
                return movie;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find movie by id in offline TMDB movies; {ex}", ex.Message);
        }
        return null;
    }

    public IEnumerable<TMDBOfflineSearch_Movie> SearchMovies(string query, bool fuzzy = true)
    {
        try
        {
            EnsureMoviesAreUsable();
            if (_movies != null)
                return _movies.Values
                    .AsParallel()
                    .Search(
                        query,
                        anime => new string[] { anime.Title },
                        fuzzy
                    )
                    .Select(a => a.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search offline TMDB movies; {ex}", ex.Message);
        }
        return Array.Empty<TMDBOfflineSearch_Movie>();
    }

    private void EnsureMoviesAreUsable()
    {
        if (_movies == null && !File.Exists(CachedMovieFilePath))
        {
            lock (s_accessLock)
            {
                DownloadMovies();
                return;
            }
        }

        // If data is stale, then re-download.
        lock (s_accessLock)
        {
            var lastWriteTime = File.GetLastWriteTime(CachedMovieFilePath);
            if (DateTime.Now - lastWriteTime > TimeSpan.FromHours(24))
            {
                DownloadMovies();
                return;
            }
        }

        LoadMovies();
    }

    private void DownloadMovies()
    {
        if (File.Exists(CachedMovieFilePath + ".tmp"))
            File.Delete(CachedMovieFilePath + ".tmp");

        // Download the file
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var movies = DownloadList<TMDBOfflineSearch_Movie>(string.Format(Constants.URLS.TMDB_Export, "movie", yesterday.Month.ToString("00"), yesterday.Day.ToString("00"), yesterday.Year.ToString("0000")));
        if (movies == null)
            return;

        // We have a fallback since we don't know when they will be adding the daily exports that supposedly will be running for the adult movies.
        var nsfwMovies = DownloadList<TMDBOfflineSearch_Movie>(string.Format(Constants.URLS.TMDB_Export, "adult_movie", yesterday.Month.ToString("00"), yesterday.Day.ToString("00"), yesterday.Year.ToString("0000"))) ??
            DownloadList<TMDBOfflineSearch_Movie>(string.Format(Constants.URLS.TMDB_Export, "adult_movie", "07", "05", "2023"));
        if (nsfwMovies == null)
            return;

        movies.AddRange(nsfwMovies);

        var text = JsonConvert.SerializeObject(movies);

        if (File.Exists(CachedMovieFilePath))
            File.Delete(CachedMovieFilePath);

        File.WriteAllText(text, CachedMovieFilePath);

        _movies = movies.ToDictionary(movie => movie.ID);
    }

    private void LoadMovies()
    {
        if (_movies != null)
            return;

        using var stream = new FileStream(CachedMovieFilePath, FileMode.Open);
        var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
        var textResponse = new StreamReader(gzipStream).ReadToEnd();
        var result = JsonConvert.DeserializeObject<List<TMDBOfflineSearch_Movie>>(textResponse);
        if (result == null)
            return;

        _movies = result.ToDictionary(movie => movie.ID);
    }

    #endregion

    #region Show

    private static string CachedShowFilePath => Path.Combine(Utils.ApplicationPath, "tmdb-shows.json.gz");

    private IDictionary<int, TMDBOfflineSearch_Show>? _shows = null;

    public IEnumerable<TMDBOfflineSearch_Show> GetAllShows()
    {
        try
        {
            EnsureShowsAreUsable();
            if (_shows != null)
                return _shows.Values;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all offline TMDB shows; {ex}", ex.Message);
        }
        return Array.Empty<TMDBOfflineSearch_Show>();
    }

    public TMDBOfflineSearch_Show? FindShowByID(int showId)
    {
        try
        {
            EnsureShowsAreUsable();
            if (_shows != null && _shows.TryGetValue(showId, out var show))
                return show;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find show by id in offline TMDB shows; {ex}", ex.Message);
        }
        return null;
    }

    public IEnumerable<TMDBOfflineSearch_Show> SearchShows(string query, bool fuzzy = true)
    {
        try
        {
            EnsureShowsAreUsable();
            if (_shows != null)
                return _shows.Values
                    .AsParallel()
                    .Search(
                        query,
                        anime => new string[] { anime.Title },
                        fuzzy
                    )
                    .Select(a => a.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search offline TMDB shows; {ex}", ex.Message);
        }
        return Array.Empty<TMDBOfflineSearch_Show>();
    }

    private void EnsureShowsAreUsable()
    {
        if (_shows == null && !File.Exists(CachedShowFilePath))
        {
            lock (s_accessLock)
            {
                DownloadShows();
                return;
            }
        }

        // If data is stale, then re-download.
        lock (s_accessLock)
        {
            var lastWriteTime = File.GetLastWriteTime(CachedShowFilePath);
            if (DateTime.Now - lastWriteTime > TimeSpan.FromHours(24))
            {
                DownloadShows();
                return;
            }
        }

        LoadShows();
    }

    private void DownloadShows()
    {
        // Download the file
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var shows = DownloadList<TMDBOfflineSearch_Show>(string.Format(Constants.URLS.TMDB_Export, "tv_series", yesterday.Month.ToString("00"), yesterday.Day.ToString("00"), yesterday.Year.ToString("0000")));
        if (shows == null)
            return;

        // We have a fallback since we don't know when they will be adding the daily exports that supposedly will be running for the adult shows.
        var nsfwShows = DownloadList<TMDBOfflineSearch_Show>(string.Format(Constants.URLS.TMDB_Export, "adult_tv_series", yesterday.Month.ToString("00"), yesterday.Day.ToString("00"), yesterday.Year.ToString("0000"))) ??
            DownloadList<TMDBOfflineSearch_Show>(string.Format(Constants.URLS.TMDB_Export, "adult_tv_series", "07", "05", "2023"));
        if (nsfwShows == null)
            return;

        // Monkey-patch the nsfw shows since the field is not in the raw list.
        foreach (var show in nsfwShows)
            show.IsRestricted = true;

        shows.AddRange(nsfwShows);

        var text = JsonConvert.SerializeObject(shows);

        if (File.Exists(CachedShowFilePath))
            File.Delete(CachedShowFilePath);

        File.WriteAllText(text, CachedShowFilePath);

        _shows = shows.ToDictionary(show => show.ID);
    }

    private void LoadShows()
    {
        if (_shows != null)
            return;

        using var stream = new FileStream(CachedShowFilePath, FileMode.Open);
        var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
        var textResponse = new StreamReader(gzipStream).ReadToEnd();
        var result = JsonConvert.DeserializeObject<List<TMDBOfflineSearch_Show>>(textResponse);
        if (result == null)
            return;

        _shows = result.ToDictionary(show => show.ID);
    }

    #endregion

    private List<T>? DownloadList<T>(string url)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.UserAgent.Add(new("Shoko Server", Utils.GetApplicationVersion()));
            var stream = client.GetStreamAsync(url).Result;
            if (stream == null)
                return null;

            var gzip = new GZipStream(stream, CompressionMode.Decompress);
            var textResponse = new StreamReader(gzip).ReadToEnd();
            if (string.IsNullOrEmpty(textResponse))
                return null;

            return textResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => JsonConvert.DeserializeObject<T>(line)!)
                .Where(movie => movie != null)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to download list; {Message}", ex.Message);
            return null;
        }
    }
}
