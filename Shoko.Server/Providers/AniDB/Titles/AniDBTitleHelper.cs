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
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Providers.AniDB.Titles;

public class AniDBTitleHelper
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly ReaderWriterLockSlim _accessLock = new(LockRecursionPolicy.SupportsRecursion);
    private readonly string _cacheFilePath = Path.Combine(Utils.ApplicationPath, "anime-titles.xml");
    private readonly string _cacheFilePathTemp = Path.Combine(Utils.ApplicationPath, "anime-titles.xml") + ".temp";
    private readonly string _cacheFilePathBak = Path.Combine(Utils.ApplicationPath, "anime-titles.xml") + ".bak";
    private ResponseAniDBTitles _cache;

    public AniDBTitleHelper(ISettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;
    }

    public IEnumerable<ResponseAniDBTitles.Anime> GetAll()
    {
        try
        {
            if (_cache == null) CreateCache();

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
            if (_cache == null) CreateCache();

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
            if (_cache == null) CreateCache();


            try
            {
                _accessLock.EnterReadLock();
                var languages = _settingsProvider.GetSettings().Language.SeriesTitleLanguageOrder;
                return _cache?.AnimeList
                    .AsParallel()
                    .Search(
                        query,
                        anime => anime.Titles
                            .Where(a => a.TitleType == TitleType.Main || a.Language == TitleLanguage.English || a.Language == TitleLanguage.Romaji ||
                                        languages.Contains(a.LanguageCode))
                            .Select(a => a.Title)
                            .ToList(),
                        fuzzy
                    )
                    .ToList() ?? [];
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

    private void CreateCache()
    {
        _accessLock.EnterWriteLock();
        if (!File.Exists(_cacheFilePath))
        {
            // first check if there's a temp file
            if (File.Exists(_cacheFilePathTemp)) File.Move(_cacheFilePathTemp, _cacheFilePath);

            if (!File.Exists(_cacheFilePath)) DownloadCache();
        }

        if (!File.Exists(_cacheFilePath))
        {
            _accessLock.ExitWriteLock();
            return;
        }

        // If data is stale, then re-download
        var lastWriteTime = File.GetLastWriteTime(_cacheFilePath);
        if (DateTime.Now - lastWriteTime > TimeSpan.FromHours(24)) DownloadCache();

        try
        {
            LoadCache();
        }
        catch
        {
            Decompress();
            LoadCache();
        }
        _accessLock.ExitWriteLock();
    }

    private void LoadCache()
    {
        // Load the file
        using var stream = new FileStream(_cacheFilePath, FileMode.Open);
        var serializer = new XmlSerializer(typeof(ResponseAniDBTitles));
        if (serializer.Deserialize(stream) is ResponseAniDBTitles rawData) _cache = rawData;
    }

    private void Decompress()
    {
        using var stream = new FileStream(_cacheFilePath, FileMode.Open);
        var gzip = new GZipStream(stream, CompressionMode.Decompress);
        var textResponse = new StreamReader(gzip).ReadToEnd();
        if (File.Exists(_cacheFilePathTemp)) File.Delete(_cacheFilePathTemp);

        File.WriteAllText(_cacheFilePathTemp, textResponse);

        // backup the old one
        if (File.Exists(_cacheFilePath)) File.Move(_cacheFilePath, _cacheFilePathBak);

        // rename new one
        File.Move(_cacheFilePathTemp, _cacheFilePath);

        // remove old one
        if (File.Exists(_cacheFilePathBak)) File.Delete(_cacheFilePathBak);
    }

    private void DownloadCache()
    {
        try
        {
            if (File.Exists(_cacheFilePathTemp)) File.Delete(_cacheFilePathTemp);

            // Ignore all certificate failures.
            ServicePointManager.Expect100Continue = true;
            //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            //ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            // Download the file
            using var httpClient = new HttpClient(new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = delegate { return true; }
                }
            });
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:74.0) Gecko/20100101 Firefox/74.0");
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "de,en-US;q=0.7,en;q=0.3");
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip,deflate");

            using var response = httpClient.GetAsync(Constants.AniDBTitlesURL).Result;
            if (response.IsSuccessStatusCode)
            {
                using var responseStream = response.Content.ReadAsStream();
                using var gzipStream = new GZipStream(responseStream, CompressionMode.Decompress);
                using var reader = new StreamReader(gzipStream);
                var textResponse = reader.ReadToEnd();
                File.WriteAllText(_cacheFilePathTemp, textResponse);
            }
            else
            {
                Console.WriteLine($@"Failed to download file. Status code: {response.StatusCode}");
                return;
            }

            // backup the old one
            if (File.Exists(_cacheFilePath)) File.Move(_cacheFilePath, _cacheFilePathBak);

            // rename new one
            File.Move(_cacheFilePathTemp, _cacheFilePath);

            // remove old one
            if (File.Exists(_cacheFilePathBak)) File.Delete(_cacheFilePathBak);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
