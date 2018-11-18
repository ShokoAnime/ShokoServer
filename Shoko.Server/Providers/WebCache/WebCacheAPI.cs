using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using NLog;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Models.Server.CrossRef;
using Shoko.Models.WebCache;
using Shoko.Server.Settings;

namespace Shoko.Server.Providers.WebCache
{
    public class WebCacheAPI
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private static ThreadLocal<WebCacheAPI> instances = new ThreadLocal<WebCacheAPI>(() => new WebCacheAPI());
        public static WebCacheAPI Instance => instances.Value;
        private static Regex ban = new Regex("Banned:\\s(.*?)\\sExpiration:\\s(.*)", RegexOptions.Compiled);
        private WebCacheAPI()
        {
        }

        private readonly Client cclient = new Client(ServerSettings.Instance.WebCache.Address, new HttpClient());

        private string GetToken()
        {
            if (ServerSettings.Instance.WebCache.Session == null)
                return null;
            if (ServerSettings.Instance.WebCache.Session.Expiration.AddSeconds(-10) < DateTime.UtcNow)
                return null;
            return ServerSettings.Instance.WebCache.Session.Token;
        }
       
        private string Authenticate()
        {
            string token = GetToken();
            if (token == null)
            {
                if (ServerSettings.Instance.WebCache.BannedExpiration.HasValue && ServerSettings.Instance.WebCache.BannedExpiration.HasValue && ServerSettings.Instance.WebCache.BannedExpiration.Value > DateTime.UtcNow)
                    return null;
                CookieContainer cookieContainer = new CookieContainer();
                using (var handler = new HttpClientHandler {CookieContainer = cookieContainer})
                using (var client = new HttpClient(handler))
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36 Edge/16.16241");
                    Uri uri = new Uri("http://anidb.net/perl-bin/animedb.pl"); //MOVE TO Properties              
                    string post = $"show=userpage&xuser={HttpUtility.UrlEncode(ServerSettings.Instance.AniDb.Username)}&xpass={HttpUtility.UrlEncode(ServerSettings.Instance.AniDb.Password)}&do.auth=login";
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, uri);
                    request.Headers.Referrer = uri;
                    request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(post));
                    Uri host = new Uri(uri.Scheme + "://" + uri.Host);
                    HttpResponseMessage response = Task.Run(async () => await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)).GetAwaiter().GetResult();
                    if (response.IsSuccessStatusCode)
                    {
                        List<Cookie> cookies = cookieContainer.GetCookies(host).Cast<Cookie>().ToList();
                        if (!cookies.Any(a => a.Name == "adbsess" && !string.IsNullOrEmpty(a.Value)))
                            return null;
                        WebCache_AniDBLoggedInfo logged = new WebCache_AniDBLoggedInfo();
                        logged.Cookies = cookies.ToDictionary(a => a.Name, a => a.Value);
                        logged.UserName = ServerSettings.Instance.AniDb.Username;
                        try
                        {
                            WebCache_SessionInfo session = cclient.Verify(logged);
                            ServerSettings.Instance.WebCache.Session = session;
                            ServerSettings.Instance.SaveSettings();
                            return GetToken();

                        }
                        catch (SwaggerException e)
                        {
                            if (e.StatusCode == 403)
                            {
                                ServerSettings.Instance.WebCache.BannedReason = "Unable to login to AniDB";
                                ServerSettings.Instance.WebCache.BannedExpiration = DateTime.UtcNow.AddHours(1);
                                logger.Error("Unable to login to AniDB, waiting for 1 hour. Error:" + e);
                                return null;
                            }

                            logger.Error("Unable to login to AniDB. Error: " + e);
                            return null;
                        }

                    }

                    ServerSettings.Instance.WebCache.BannedReason = "Unable to login to AniDB";
                    ServerSettings.Instance.WebCache.BannedExpiration = DateTime.UtcNow.AddHours(1);
                    logger.Error("Unable to login to AniDB, waiting for 1 hour");
                }

                return null;
            }

            return token;
        }

        private bool WrapAuthentication(Action<string> act)
        {
            int retries = 0;
            do
            {
                string token = Authenticate();
                if (token == null)
                    return false;
                try
                {
                    act(token);
                    return true;
                }
                catch (SwaggerException e)
                {
                    if (e.StatusCode == 403)
                    {
                        if (e.Response.Contains("Banned"))
                        {
                            Match m = ban.Match(e.Response);
                            if (m.Success)
                            {
                                ServerSettings.Instance.WebCache.BannedReason = m.Groups[1].Value;
                                DateTime dt = DateTime.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                                ServerSettings.Instance.WebCache.BannedExpiration = dt;
                                return false;
                            }
                        }
                    }

                    if (e.StatusCode == 404)
                        return true;
                }

                retries++;
            } while (retries<=3);

            return false;
        }
        private T WrapAuthentication<T>(Func<string,T> act) where T: class
        {
            int retries = 0;
            do
            {
                string token = Authenticate();
                if (token == null)
                    return null;
                try
                {
                    return act(token);
                }
                catch (SwaggerException e)
                {
                    if (e.StatusCode == 403)
                    {
                        if (e.Response.Contains("Banned"))
                        {
                            Match m = ban.Match(e.Response);
                            if (m.Success)
                            {
                                ServerSettings.Instance.WebCache.BannedReason = m.Groups[1].Value;
                                DateTime dt = DateTime.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                                ServerSettings.Instance.WebCache.BannedExpiration = dt;
                                return null;
                            }
                        }
                    }

                    if (e.StatusCode == 404)
                        return null;
                }

                retries++;
            } while (retries <= 3);
            return null;
        }
        public bool AddHash(IEnumerable<WebCache_FileHash> hashes)
        {
            return WrapAuthentication((token) =>
            {
                cclient.AddHashesAsync(token, hashes).GetAwaiter().GetResult();
            });
        }

        public bool RefreshToken()
        {
            WebCache_SessionInfo ws=WrapAuthentication((token) => cclient.RefreshSession(token));
            if (ws != null)
            { 
                ServerSettings.Instance.WebCache.Session = ws;
                ServerSettings.Instance.SaveSettings();
                return true;
            }
            return false;
        }

        public WebCache_FileHash GetHash(WebCache_HashType type, string hash, long size=0)
        {
            return WrapAuthentication((token) => cclient.GetHash(token,(int)type,hash,size));
        }
        public bool AddHashes(IEnumerable<WebCache_FileHash> hashes)
        {
            return WrapAuthentication((token) =>
            {
                cclient.AddHashes(token, hashes);
            });
        }


        public List<WebCache_FileHash_Collision_Info> GetCollisions()
        {
            return WrapAuthentication((token) => cclient.GetCollisions(token));
        }
        public bool ApproveCollision(int id)
        {
            return WrapAuthentication((token) =>
            {
                cclient.ApproveCollision(token,id);
            });
        }
        public WebCache_Media GetMediaInfo(string ed2k)
        {
            return WrapAuthentication((token) => cclient.GetMediaInfo(token, ed2k));
        }

        public bool AddMediaInfo(WebCache_Media media)
        {
            return WrapAuthentication((token) =>
            {
                cclient.AddMediaInfo(token, media);
            });
        }

        public List<WebCache_CrossRef_AniDB_Provider> GetCrossRef_AniDB_Provider(int animeId, CrossRefType type)
        {
            return WrapAuthentication((token) => cclient.GetProvider(token, animeId,(int)type));
        }
        public List<WebCache_CrossRef_AniDB_Provider> GetRandomCrossRef_AniDB_Provider(CrossRefType type)
        {
            return WrapAuthentication((token) => cclient.GetRandomProvider(token, (int)type));
        }
        public bool DeleteCrossRef_AniDB_Provider(int animeId, CrossRefType type)
        {
            return WrapAuthentication((token) =>
            {
                cclient.DeleteProvider(token,animeId, (int)type);
            });
        }

        public bool AddCrossRef_AniDB_Provider(CrossRef_AniDB_Provider cross, bool approve)
        {
            return WrapAuthentication((token) =>
            {
                cclient.AddProvider(cross,token,approve);
            });
        }
        public bool ManageCrossRef_AniDB_Provider(int id, bool approve)
        {
            return WrapAuthentication((token) =>
            {
                cclient.AddProviderManage(token,id,approve);
            });
        }
        public CrossRef_File_Episode GetCrossRef_File_Episode(string hash)
        {
            return WrapAuthentication((token) => cclient.GetFileEpisode(token, hash));

        }
        public bool DeleteCrossRef_File_Episode(string hash)
        {
            return WrapAuthentication((token) =>
            {
                cclient.DeleteFileEpisode(token, hash);
            });
        }
        public bool AddCrossRef_File_Episode(CrossRef_File_Episode episode)
        {
            return WrapAuthentication((token) =>
            {
                cclient.AddFileEpisode(token, episode);
            });
        }

    }
}
