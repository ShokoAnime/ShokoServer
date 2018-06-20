using System;
using System.Collections.Concurrent;
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
using NLog;
using Shoko.Commons.Utils;
using Shoko.Models.Plex;
using Shoko.Models.Plex.Connections;
using Shoko.Models.Plex.Login;
using Shoko.Models.Server;
using Shoko.Server.Repositories;
using Directory = Shoko.Models.Plex.Libraries.Directory;
using MediaContainer = Shoko.Models.Plex.Connections.MediaContainer;

namespace Shoko.Server.Plex
{
    public class PlexHelper
    {
        private const string ClientIdentifier = "d14f0724-a4e8-498a-bb67-add795b38331";
        private static readonly HttpClient HttpClient = new HttpClient();

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly ConcurrentDictionary<int, PlexHelper> Cache = new ConcurrentDictionary<int, PlexHelper>();

        private readonly int _userId;
        private JMMUser _user { get => RepoFactory.JMMUser.GetByID(_userId); }

        internal readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings();

        private Connection _cachedConnection;
        private PlexKey _key;
        private DateTime _lastCacheTime = DateTime.MinValue;
        private DateTime _lastMediaCacheTime = DateTime.MinValue;


        private MediaDevice _mediaDevice;

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

        private PlexHelper(JMMUser user)
        {
            _userId = user.JMMUserID;
            SerializerSettings.Converters.Add(new PlexConverter(this));
        }

        public MediaDevice ServerCache
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
                _mediaDevice = GetPlexServers().FirstOrDefault(s =>
                    s.Connection.Any(c => c.Address == strings[0] && c.Port == strings[1]));
                if (_mediaDevice != null)
                    ServerSettings.Plex_Server = _mediaDevice.ClientIdentifier;
                return _mediaDevice;
            }
            private set
            {
                _mediaDevice = value;
                _lastMediaCacheTime = DateTime.Now;
            }
        }

        private Connection ConnectionCache
        {
            get
            {
                if (DateTime.Now - TimeSpan.FromHours(12) < _lastCacheTime && _cachedConnection != null)
                    return _cachedConnection;

                //foreach (var connection in ServerCache.Connection)
                Parallel.ForEach(ServerCache.Connection, (connection, state) =>
                        {
                            try
                            {
                                if (state.ShouldExitCurrentIteration) return;
                                var (result, _) = RequestAsync($"{connection.Uri}/library/sections", HttpMethod.Get,
                                        new Dictionary<string, string> {{"X-Plex-Token", ServerCache.AccessToken}})
                                    .Result;

                                if (result != HttpStatusCode.OK)
                                {
                                    Logger.Trace($"Got response from: {connection.Uri} {result}");
                                    return;
                                }

                                _cachedConnection = connection;
                                state.Stop();
                            }
                            catch (AggregateException)
                            {
                                Logger.Trace($"Failed connection to: {connection.Uri}");
                            }
                        }
                    );

                _lastCacheTime = DateTime.Now;

                return _cachedConnection;
            }
        }

        private Dictionary<string, string> AuthenticationHeaders => new Dictionary<string, string>
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
                                   AuthenticationHeaders).ConfigureAwait(false)
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

        private PlexKey GetPlexKey()
        {
            if (_key != null)
            {
                if (_key.ExpiresAt > DateTime.Now)
                    return _key;

                if (_key.ExpiresAt <= DateTime.Now)
                    _key = null;
            }

            var (_, content) = RequestAsync("https://plex.tv/api/v2/pins?strong=true", HttpMethod.Post).Result;
            _key = JsonConvert.DeserializeObject<PlexKey>(content);
            return _key;
        }

        private string GetPlexToken()
        {
            if (!string.IsNullOrEmpty(_user?.PlexToken))
                return _user.PlexToken;

            if (_key == null) GetPlexKey();

            if (_key.AuthToken != null) return _key?.AuthToken;

            var (_, content) = RequestAsync($"https://plex.tv/api/v2/pins/{_key.Id}", HttpMethod.Get).Result;
            try
            {
                _key = JsonConvert.DeserializeObject<PlexKey>(content);
            }
            catch
            {
                Logger.Trace($"Unable to deserialize Plex Key from server. Response was \n{content}");
            }

            if (_key == null) return null;
            _user.PlexToken = _key.AuthToken;

            new ShokoServiceImplementation().SaveUser(_user);
            return _user.PlexToken;
        }

        public static PlexHelper GetForUser(JMMUser user) => Cache.GetOrAdd(user.JMMUserID, u => new PlexHelper(user));

        private async Task<(HttpStatusCode status, string content)> RequestAsync(string url, HttpMethod method,
            IDictionary<string, string> headers = default, string content = null, bool xml = false,
            Action<HttpRequestMessage> configureRequest = null)
        {
            //headers["Accept"] = xml ? "application/xml" : "application/json";
            Logger.Trace($"Requesting from plex: {method.Method} {url}");

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
            Logger.Trace($"Got response: {resp.StatusCode}");
            return (resp.StatusCode, await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
        }

        private MediaDevice[] GetPlexDevices()
        {
            if (!IsAuthenticated) return new MediaDevice[0];
            var (_, content) = RequestAsync("https://plex.tv/api/resources?includeHttps=1", HttpMethod.Get,
                AuthenticationHeaders).Result;
            var serializer = new XmlSerializer(typeof(MediaContainer));
            using (TextReader reader = new StringReader(content))
            {
                try
                {
                    return ((MediaContainer) serializer.Deserialize(reader)).Device;
                }
                catch
                {
                    Logger.Trace($"Unable to deserialize Plex Devices from server. Response was \n{reader}");
                    return new MediaDevice[0];
                }
            }
        }

        public List<MediaDevice> GetPlexServers()
        {
            if (!IsAuthenticated) return new List<MediaDevice>();
            return GetPlexDevices().Where(d => d.Provides.Split(',').Contains("server")).ToList();
        }

        public void UseServer(MediaDevice server)
        {
            if (server == null)
            {
                ServerSettings.Plex_Server = null;
                return;
            }

            if (!server.Provides.Split(',').Contains("server")) return; //not allowed.

            ServerSettings.Plex_Server = server.ClientIdentifier;
            ServerCache = server;
        }

        public User GetAccount()
        {
            //https://plex.tv/users/account.json
            var (resp, data) =
                RequestAsync("https://plex.tv/users/account.json", HttpMethod.Get, AuthenticationHeaders)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
            if (resp != HttpStatusCode.OK) return null;
            return JsonConvert.DeserializeObject<PlexAccount>(data).User;
        }


        public Directory[] GetDirectories()
        {
            if (ServerCache == null) return null;
            var (_, data) = RequestFromPlexAsync("/library/sections").Result;
            return JsonConvert
                .DeserializeObject<MediaContainer<Shoko.Models.Plex.Libraries.MediaContainer>>(data, SerializerSettings)
                .Container.Directory ?? new Directory[0];
        }

        public async Task<(HttpStatusCode status, string content)> RequestFromPlexAsync(string path,
            HttpMethod method = null)
        {
            return await RequestAsync($"{ConnectionCache.Uri}{path}", method ?? HttpMethod.Get,
                    new Dictionary<string, string> {{"X-Plex-Token", ServerCache.AccessToken}})
                .ConfigureAwait(false);
        }

        public void InvalidateToken()
        {
            _user.PlexToken = string.Empty;
            new ShokoServiceImplementation().SaveUser(_user);
        }
    }
}