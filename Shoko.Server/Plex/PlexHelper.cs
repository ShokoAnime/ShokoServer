using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Shoko.Commons.Utils;
using Shoko.Models.Server;

namespace Shoko.Server.Plex
{
    public class PlexHelper
    {
        private readonly JMMUser _user;
        private static readonly HttpClient HttpClient = new HttpClient();

        static PlexHelper()
        {
            HttpClient.DefaultRequestHeaders.Add("X-Plex-Client-Identifier", ClientIdentifier);
            HttpClient.DefaultRequestHeaders.Add("X-Plex-Platform-Version", ServerState.Instance.ApplicationVersion);
            HttpClient.DefaultRequestHeaders.Add("X-Plex-Platform", "Shoko Server");
            HttpClient.DefaultRequestHeaders.Add("X-Plex-Device-Name", "Shoko Server Sync");
            HttpClient.DefaultRequestHeaders.Add("X-Plex-Device", "Shoko");
            HttpClient.DefaultRequestHeaders.Add("User-Agent",
                $"{Assembly.GetEntryAssembly().GetName().Name} v${Assembly.GetEntryAssembly().GetName().Version}");
            HttpClient.Timeout = TimeSpan.FromSeconds(3);
        }


        private Commons.Plex.Connections.MediaDevice _mediaDevice;
        private DateTime _lastMediaCacheTime = DateTime.MinValue;

        private Commons.Plex.Connections.MediaDevice ServerCache
        {
            get
            {
                if (string.IsNullOrEmpty(ServerSettings.Plex_Server)) return null;
                if (DateTime.Now - TimeSpan.FromHours(1) >= _lastMediaCacheTime) _mediaDevice = null;
                if (_mediaDevice != null && ServerSettings.Plex_Server == _mediaDevice.ClientIdentifier)
                    return _mediaDevice;
                _mediaDevice = GetPlexServers().FirstOrDefault(s => s.ClientIdentifier == ServerSettings.Plex_Server);
                if (_mediaDevice != null) return _mediaDevice;
                if (!ServerSettings.Plex_Server.Contains(':')) return null;


                var strings = ServerSettings.Plex_Server.Split(':');
                _mediaDevice = GetPlexServers().FirstOrDefault(s => s.Connection.Any(c => c.Address == strings[0] && c.Port == strings[1]));
                ServerSettings.Plex_Server = _mediaDevice.ClientIdentifier;
                return _mediaDevice;
            }
            set
            {
                _mediaDevice = value;
                _lastMediaCacheTime = DateTime.Now;
            }
        }

        private Commons.Plex.Connections.Connection ConnectionCache
        {
            get
            {
                if (DateTime.Now - TimeSpan.FromHours(12) < _lastCacheTime && _cachedConnection != null)
                    return _cachedConnection;

                foreach (var connection in ServerCache.Connection)
                {
                    try
                    {
                        var result = RequestAsync($"{connection.Uri}/library/sections", HttpMethod.Get,
                                headers: new Dictionary<string, string> {{"X-Plex-Token", ServerCache.AccessToken}})
                            .Result;

                        if (result.status != HttpStatusCode.OK) continue;
                        _cachedConnection = connection;
                        break;
                    }
                    catch (AggregateException)
                    {
                    }
                }

                _lastCacheTime = DateTime.Now;

                return _cachedConnection;
            }
        }

        private Commons.Plex.Connections.Connection _cachedConnection;
        private DateTime _lastCacheTime = DateTime.MinValue;

        private Dictionary<string, string> AuthenticationHeaders => new Dictionary<string, string>()
        {
            {"X-Plex-Token", GetPlexToken()}
        };

        public bool IsAuthenticated
        {
            get
            {
                try
                {
                    return RequestAsync("https://plex.tv/users/account.json", HttpMethod.Get,
                                   headers: AuthenticationHeaders).ConfigureAwait(false)
                               .GetAwaiter().GetResult().status == HttpStatusCode.OK;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public string LoginUrl => $"https://app.plex.tv/auth#?clientID={ClientIdentifier}" +
                                  $"&code={GetPlexKey().Code}" +
                                  "&context%5Bdevice%5D%5Bproduct%5D=Shoko%20Server" +
                                  $"&context%5Bdevice%5D%5Bplatform%5D={WebUtility.UrlEncode(Environment.OSVersion.Platform.ToString())}" +
                                  $"&context%5Bdevice%5D%5BplatformVersion%5D={WebUtility.UrlEncode(Environment.OSVersion.VersionString)}" +
                                  $"&context%5Bdevice%5D%5Bversion%5D={WebUtility.UrlEncode(Assembly.GetEntryAssembly().GetName().Version.ToString())}";

        private Commons.Plex.Login.PlexKey GetPlexKey()
        {
            if (_key != null) return _key;

            var (_, content) = RequestAsync("https://plex.tv/api/v2/pins?strong=true", HttpMethod.Post).Result;
            _key = JsonConvert.DeserializeObject<Commons.Plex.Login.PlexKey>(content);
            return _key;
        }

        private string GetPlexToken()
        {
            if (!string.IsNullOrEmpty(_user?.PlexToken))
                return _user.PlexToken;

            if (_key == null) GetPlexKey();

            if (_key.AuthToken != null) return _key?.AuthToken;

            var (_, content) = RequestAsync($"https://plex.tv/api/v2/pins/{_key.Id}", HttpMethod.Get).Result;
            _key = JsonConvert.DeserializeObject<Commons.Plex.Login.PlexKey>(content);
            if (_key == null) return null;
            _user.PlexToken = _key.AuthToken;

            new ShokoServiceImplementation().SaveUser(_user);
            return _user.PlexToken;
        }


        //private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private Commons.Plex.Login.PlexKey _key;
        private const string ClientIdentifier = "d14f0724-a4e8-498a-bb67-add795b38331";
        private static readonly Dictionary<int, PlexHelper> Cache = new Dictionary<int, PlexHelper>();
        internal readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings();

        public static PlexHelper GetForUser(JMMUser user)
        {
            if (Cache.TryGetValue(user.JMMUserID, out var helper)) return helper;
            Cache.Add(user.JMMUserID, helper = new PlexHelper(user));
            return helper;
        }

        private PlexHelper(JMMUser user)
        {
            _user = user;
            SerializerSettings.Converters.Add(new PlexConverter(this));

        }

        private async Task<(HttpStatusCode status, string content)> RequestAsync(string url, HttpMethod method,
            IDictionary<string, string> headers = default, string content = null, bool xml = false,
            Action<HttpRequestMessage> configureRequest = null)
        {
            //headers["Accept"] = xml ? "application/xml" : "application/json";

            var req = new HttpRequestMessage(method, url);
            if (method == HttpMethod.Post) req.Content = new StringContent(content ?? "");

            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(xml ? "application/xml" : "application/json"));
            if (headers != null)
                foreach (var (header, val) in headers)
                {
                    if (req.Headers.Contains(header)) req.Headers.Remove(header);
                    req.Headers.Add(header, val);
                }

            configureRequest?.Invoke(req);

            var resp = await HttpClient.SendAsync(req).ConfigureAwait(false);
            return (resp.StatusCode, await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
        }

        private List<Commons.Plex.Connections.MediaDevice> GetPlexDevices()
        {
            var (_, content) = RequestAsync("https://plex.tv/api/resources?includeHttps=1", HttpMethod.Get,
                AuthenticationHeaders).Result;
            XmlSerializer serializer = new XmlSerializer(typeof(Commons.Plex.Connections.MediaContainer));
            using (TextReader reader = new StringReader(content))
                return ((Commons.Plex.Connections.MediaContainer) serializer.Deserialize(reader)).Device;
        }

        private List<Commons.Plex.Connections.MediaDevice> GetPlexServers() =>
            GetPlexDevices().Where(d => d.Provides.Split(',').Contains("server")).ToList();

        public void UseServer(Commons.Plex.Connections.MediaDevice server)
        {
            if (!server.Provides.Split(',').Contains("server")) return; //not allowed.

            ServerSettings.Plex_Server = server.ClientIdentifier;
            ServerCache = server;
        }

        public Commons.Plex.Login.User GetAccount()
        {
            //https://plex.tv/users/account.json
            var (resp, data) =
                RequestAsync("https://plex.tv/users/account.json", HttpMethod.Get, headers: AuthenticationHeaders)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
            if (resp != HttpStatusCode.OK) return null;
            return JsonConvert.DeserializeObject<Commons.Plex.Login.PlexAccount>(data).User;
        }


        public Commons.Plex.Libraries.Directory[] GetDirectories()
        {
            if (ServerCache == null) return null;
            var (_, data) = RequestFromPlexAsync("/library/sections").Result;
            return JsonConvert
                .DeserializeObject<Commons.Plex.MediaContainer<Commons.Plex.Libraries.MediaContainer>>(data, SerializerSettings)
                .Container.Directory;
        }

        public async Task<(HttpStatusCode status, string content)> RequestFromPlexAsync(string path,
            HttpMethod method = null) =>
            await RequestAsync($"{ConnectionCache.Uri}{path}", method ?? HttpMethod.Get,
                    headers: new Dictionary<string, string> {{"X-Plex-Token", ServerCache.AccessToken}})
                .ConfigureAwait(false);

        public void InvalidateToken()
        {
            _user.PlexToken = string.Empty;
            new ShokoServiceImplementation().SaveUser(_user);
        }
    }
}