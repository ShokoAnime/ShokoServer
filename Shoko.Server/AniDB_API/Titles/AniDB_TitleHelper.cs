using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Xml.Serialization;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.AniDB_API.Titles
{
    public class AniDB_TitleHelper
    {
        // ensure that it doesn't try to download at the same time
        private static readonly object AccessLock = new();

        private static readonly string CacheFilePath = Path.Combine(ServerSettings.ApplicationPath, "anime-titles.xml");
        
        private static readonly string CacheFilePathTemp =
            Path.Combine(ServerSettings.ApplicationPath, "anime-titles.xml") + ".temp";

        private static readonly string CacheFilePathBak =
            Path.Combine(ServerSettings.ApplicationPath, "anime-titles.xml") + ".bak";

        private AniDBRaw_AnimeTitles _cache;

        private static AniDB_TitleHelper _instance;

        public static AniDB_TitleHelper Instance => _instance ??= new AniDB_TitleHelper();

        public AniDBRaw_AnimeTitle_Anime SearchAnimeID(int animeID)
        {
            try
            {
                if (_cache == null) CreateCache();
                return _cache?.Animes
                    .FirstOrDefault(a => a.AnimeID == animeID);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return null;
        }

        public bool TrySearchAnimeID(int animeID, out AniDBRaw_AnimeTitle_Anime anime)
        {
            try
            {
                if (_cache == null) CreateCache();
                anime = _cache?.Animes
                    .FirstOrDefault(a => a.AnimeID == animeID);
                return anime != null;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                anime = null;
                return false;
            }
        }

        public IEnumerable<AniDBRaw_AnimeTitle_Anime> SearchTitle(string query)
        {
            try
            {
                if (_cache == null) CreateCache();
                if (_cache != null)
                    return SeriesSearch.SearchCollection(
                        query, _cache.Animes,
                        anime => anime.Titles
                            .Where(a => a.TitleLanguage.Equals("en") || a.TitleLanguage.Equals("x-jat") || ServerSettings.Instance.LanguagePreference.Contains(a.TitleLanguage))
                            .Select(a => a.Title)
                            .ToList()
                        )
                        .Select(a => a.Result);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return new List<AniDBRaw_AnimeTitle_Anime>();
        }

        private void CreateCache()
        {
            if (!File.Exists(CacheFilePath))
            {
                // first check if there's a temp file
                if (File.Exists(CacheFilePathTemp))
                {
                    File.Move(CacheFilePathTemp, CacheFilePath);
                }
                if (!File.Exists(CacheFilePath))
                    lock (AccessLock) DownloadCache();
            }
            
            if (!File.Exists(CacheFilePath)) return;

            lock (AccessLock)
            {
                // If data is stale, then re-download
                DateTime lastWriteTime = File.GetLastWriteTime(CacheFilePath);
                if (DateTime.Now - lastWriteTime > TimeSpan.FromHours(24))
                {
                    DownloadCache();
                }
            }

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

        private void LoadCache()
        {
            // Load the file
            using var stream = new FileStream(CacheFilePath, FileMode.Open);
            XmlSerializer serializer = new XmlSerializer(typeof(AniDBRaw_AnimeTitles));
            if (serializer.Deserialize(stream) is AniDBRaw_AnimeTitles rawData)
            {
                _cache = rawData;
            }
        }

        private static void Decompress()
        {
            using var stream = new FileStream(CacheFilePath, FileMode.Open);
            GZipStream gzip = new GZipStream(stream, CompressionMode.Decompress);
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

        private static void DownloadCache()
        {
            try
            {
                if (File.Exists(CacheFilePathTemp)) File.Delete(CacheFilePathTemp);
                
                // Ignore all certificate failures.
                ServicePointManager.Expect100Continue = true;                
                //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                //ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                
                // Download the file
                using (var client = new WebClient())
                {
                    client.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla / 5.0(Windows NT 10.0; Win64; x64; rv: 74.0) Gecko / 20100101 Firefox / 74.0");
                    client.Headers.Add("Accept", "text / html,application / xhtml + xml,application / xml; q = 0.9,image / webp,*/*;q=0.8");
                    client.Headers.Add("Accept-Language", "de,en-US;q=0.7,en;q=0.3");
                    client.Headers.Add("Accept-Encoding", "gzip,deflate");

                    var stream = client.OpenRead(Constants.AniDBTitlesURL);
                    if (stream != null)
                    {
                        var gzip = new GZipStream(stream, CompressionMode.Decompress);
                        var textResponse = new StreamReader(gzip).ReadToEnd();
                        File.WriteAllText(CacheFilePathTemp, textResponse);
                    }
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
    }
}