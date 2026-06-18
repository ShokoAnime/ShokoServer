using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Threading;
using System.Xml.Serialization;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Providers.AniDB.Titles;

public class AniDBTitleHelper(ISettingsProvider settingsProvider, IApplicationPaths applicationPaths)
{
    private readonly ReaderWriterLockSlim _accessLock = new(LockRecursionPolicy.SupportsRecursion);

    private string CacheFilePath => Path.Join(applicationPaths.DataPath, "anime-titles.xml");

    private string CacheFilePathTemp => CacheFilePath + ".temp";

    private string CacheFilePathBak => CacheFilePath + ".bak";

    private DateTime? _nextUpdate = null;

    private ResponseAniDBTitles _cache;

    private volatile FuzzySearchIndex<ResponseAniDBTitles.Anime> _titleIndex;

    public IEnumerable<ResponseAniDBTitles.Anime> GetAll()
    {
        try
        {
            CreateCache();

            try
            {
                _accessLock.EnterReadLock();
                return _cache?.AnimeList.ToList() ?? [];
            }
            finally
            {
                _accessLock.ExitReadLock();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return new List<ResponseAniDBTitles.Anime>();
    }

    public ResponseAniDBTitles.Anime SearchAnimeID(int animeID)
    {
        try
        {
            CreateCache();

            try
            {
                _accessLock.EnterReadLock();
                return _cache?.AnimeList
                    .FirstOrDefault(a => a.AnimeID == animeID);
            }
            finally
            {
                _accessLock.ExitReadLock();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return null;
    }

    public IEnumerable<SeriesSearch.SearchResult<ResponseAniDBTitles.Anime>> SearchTitle(string query, bool fuzzy = true)
    {
        try
        {
            CreateCache();

            try
            {
                _accessLock.EnterReadLock();
                return _titleIndex?.Search(query, fuzzy).ToList() ?? [];
            }
            finally
            {
                _accessLock.ExitReadLock();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return [];
    }

    private void RebuildTitleIndex()
    {
        if (_cache == null)
            return;

        var languages = new HashSet<string>(settingsProvider.GetSettings().Language.SeriesTitleLanguageOrder) { "en", "x-jat" };
        var idx = new FuzzySearchIndex<ResponseAniDBTitles.Anime>();
        idx.Build(
            _cache.AnimeList,
            anime => anime.Titles
                .Where(t => t.TitleType == TitleType.Main
                         || t.Language == TitleLanguage.English
                         || t.Language == TitleLanguage.Romaji
                         || languages.Contains(t.LanguageCode))
                .Select(t => t.Title)
        );
        _titleIndex = idx;
    }

    private void CreateCache()
    {
        if (_cache is not null && _nextUpdate.HasValue && DateTime.Now < _nextUpdate.Value)
            return;

        try
        {
            _accessLock.EnterWriteLock();

            if (!File.Exists(CacheFilePath))
            {
                // first check if there's a temp file
                if (File.Exists(CacheFilePathTemp))
                    File.Move(CacheFilePathTemp, CacheFilePath);

                if (!File.Exists(CacheFilePath)) DownloadCache();
            }

            if (!File.Exists(CacheFilePath)) return;

            // If data is stale, then re-download
            var lastWriteTime = File.GetLastWriteTime(CacheFilePath);
            if (DateTime.Now - lastWriteTime > TimeSpan.FromHours(24))
                DownloadCache();

            try
            {
                LoadCache();
            }
            catch
            {
                Decompress();
                LoadCache();
            }
        }
        finally
        {
            _accessLock.ExitWriteLock();
        }
    }

    private void LoadCache()
    {
        // Load the file
        using var stream = new FileStream(CacheFilePath, FileMode.Open);
        var serializer = new XmlSerializer(typeof(ResponseAniDBTitles));
        if (serializer.Deserialize(stream) is ResponseAniDBTitles rawData)
        {
            _cache = rawData;

            // Set the next update to run in 24 hours, unless we somehow failed
            // to download it in the last 24 hours, then set it to 4 hours.
            _nextUpdate = File.GetLastWriteTime(CacheFilePath).AddHours(24);
            if (_nextUpdate.Value < DateTime.Now)
                _nextUpdate = DateTime.Now.AddHours(4);

            RebuildTitleIndex();
        }
    }

    private void Decompress()
    {
        using var stream = new FileStream(CacheFilePath, FileMode.Open);
        var gzip = new GZipStream(stream, CompressionMode.Decompress);
        var textResponse = new StreamReader(gzip).ReadToEnd();
        if (File.Exists(CacheFilePathTemp)) File.Delete(CacheFilePathTemp);

        File.WriteAllText(CacheFilePathTemp, textResponse);

        // backup the old one
        if (File.Exists(CacheFilePath)) File.Move(CacheFilePath, CacheFilePathBak);

        // rename new one
        File.Move(CacheFilePathTemp, CacheFilePath);

        // remove old one
        if (File.Exists(CacheFilePathBak)) File.Delete(CacheFilePathBak);
    }

    private void DownloadCache()
    {
        try
        {
            if (File.Exists(CacheFilePathTemp)) File.Delete(CacheFilePathTemp);

            // Download the file
            using var httpClient = new HttpClient(new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = delegate { return true; }
                }
            });
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/118.0");
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "de,en-US;q=0.7,en;q=0.3");
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip,deflate");

            using var response = httpClient.GetAsync(GetTitleCacheUrl()).Result;
            if (response.IsSuccessStatusCode)
            {
                using var responseStream = response.Content.ReadAsStream();
                using var gzipStream = new GZipStream(responseStream, CompressionMode.Decompress);
                using var reader = new StreamReader(gzipStream);
                var textResponse = reader.ReadToEnd();
                File.WriteAllText(CacheFilePathTemp, textResponse);
            }
            else
            {
                Console.WriteLine($@"Failed to download file. Status code: {response.StatusCode}");
                return;
            }

            // backup the old one
            if (File.Exists(CacheFilePath)) File.Move(CacheFilePath, CacheFilePathBak);

            // rename new one
            File.Move(CacheFilePathTemp, CacheFilePath);

            // remove old one
            if (File.Exists(CacheFilePathBak)) File.Delete(CacheFilePathBak);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private string GetTitleCacheUrl()
    {
        var setting = settingsProvider.GetSettings().AniDb.TitleCacheUrl;
        if (!string.IsNullOrWhiteSpace(setting) && !string.Equals(setting, Constants.AnidbTitleCacheUrl) && (setting.StartsWith("http://") || setting.StartsWith("https://")))
            return setting;

        return Constants.AnidbTitleCacheUrl;
    }
}
