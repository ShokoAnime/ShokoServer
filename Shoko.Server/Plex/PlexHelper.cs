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
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
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

        private MediaDevice[] _plexMediaDevices;

        private MediaDevice _mediaDevice;
        private bool? isAuthenticated;

        static PlexHelper()
        {
            SetupHttpClient(HttpClient, TimeSpan.FromSeconds(3));
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
                if (string.IsNullOrEmpty(ServerSettings.Instance.Plex.Server)) return null;
                if (DateTime.Now - TimeSpan.FromHours(1) >= _lastMediaCacheTime) _mediaDevice = null;
                if (_mediaDevice != null && ServerSettings.Instance.Plex.Server == _mediaDevice.ClientIdentifier)
                    return _mediaDevice;
                _mediaDevice = GetPlexServers().FirstOrDefault(s => s.ClientIdentifier == ServerSettings.Instance.Plex.Server);
                if (_mediaDevice != null)
               {
                   _lastMediaCacheTime = DateTime.Now;
                   return _mediaDevice;
               } 
                if (!ServerSettings.Instance.Plex.Server.Contains(':')) return null;


                var strings = ServerSettings.Instance.Plex.Server.Split(':');
                _mediaDevice = GetPlexServers().FirstOrDefault(s =>
                    s.Connection.Any(c => c.Address == strings[0] && c.Port == strings[1]));
                if (_mediaDevice != null)
                    ServerSettings.Instance.Plex.Server = _mediaDevice.ClientIdentifier;
                _lastMediaCacheTime = DateTime.Now;
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
                _cachedConnection = null;

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

                                _cachedConnection = _cachedConnection ?? connection;
                                state.Break();
                            }
                            catch (AggregateException e)
                            {
                                Logger.Trace($"Failed connection to: {connection.Uri} {e}");
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
                if (isAuthenticated == true) return (bool)isAuthenticated;
                try
                {
                    isAuthenticated = RequestAsync("https://plex.tv/users/account.json", HttpMethod.Get,
                                   AuthenticationHeaders).ConfigureAwait(false)
                               .GetAwaiter().GetResult().status == HttpStatusCode.OK;
                    return (bool)isAuthenticated;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public string LoginUrl => $"https://app.plex.tv/auth#?clientID={ClientIdentifier}" +
                                  $"&code={GetPlexKey().Code}";/* +
                                  "&context%5Bdevice%5D%5Bproduct%5D=Shoko%20Server" +
                                  $"&context%5Bdevice%5D%5Bplatform%5D={WebUtility.UrlEncode(Environment.OSVersion.Platform.ToString())}" +
                                  $"&context%5Bdevice%5D%5BplatformVersion%5D={WebUtility.UrlEncode(Environment.OSVersion.VersionString)}" +
                                  $"&context%5Bdevice%5D%5Bversion%5D={WebUtility.UrlEncode(Assembly.GetEntryAssembly().GetName().Version.ToString())}";*/

        private static void SetupHttpClient(HttpClient client, TimeSpan timeout)
        {
            client.DefaultRequestHeaders.Add("X-Plex-Client-Identifier", ClientIdentifier);
            client.DefaultRequestHeaders.Add("X-Plex-Platform-Version", ServerState.Instance.ApplicationVersion);
            client.DefaultRequestHeaders.Add("X-Plex-Platform", "Shoko Server");
            client.DefaultRequestHeaders.Add("X-Plex-Device-Name", "Shoko Server Sync");
            client.DefaultRequestHeaders.Add("X-Plex-Product", "Shoko Server Sync");
            client.DefaultRequestHeaders.Add("X-Plex-Device", "Shoko");
            client.DefaultRequestHeaders.Add("User-Agent",
                $"{Assembly.GetEntryAssembly().GetName().Name} v${Assembly.GetEntryAssembly().GetName().Version}");
            client.Timeout = TimeSpan.FromSeconds(3);
        }

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
                Analytics.PostEvent("Plex", "Start Token");

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

            var client = new HttpClient();
            SetupHttpClient(client, TimeSpan.FromSeconds(60));
            var resp = await client.SendAsync(req).ConfigureAwait(false);
            Logger.Trace($"Got response: {resp.StatusCode}");
            return (resp.StatusCode, await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
        }

        private MediaDevice[] GetPlexDevices()
        {
            if (_plexMediaDevices != null)
            {
                return _plexMediaDevices;
            }

            if (!IsAuthenticated)
            {
                _plexMediaDevices = new MediaDevice[0];
                return _plexMediaDevices;
            }
            var (_, content) = RequestAsync("https://plex.tv/api/resources?includeHttps=1", HttpMethod.Get,
                AuthenticationHeaders).Result;
            var serializer = new XmlSerializer(typeof(MediaContainer));
            using (TextReader reader = new StringReader(content))
            {
                try
                {
                    return ((MediaContainer)serializer.Deserialize(reader)).Device ?? new MediaDevice[0];
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
                ServerSettings.Instance.Plex.Server = null;
                return;
            }

            if (!server.Provides.Split(',').Contains("server")) return; //not allowed.

            ServerSettings.Instance.Plex.Server = server.ClientIdentifier;
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
            if (ServerCache == null) return new Directory[0];
            try
            {
                var (_, data) = RequestFromPlexAsync("/library/sections").Result;
                return JsonConvert
                           .DeserializeObject<MediaContainer<Shoko.Models.Plex.Libraries.MediaContainer>>(data, SerializerSettings)
                           .Container.Directory ?? new Directory[0];
            }
            catch (Exception) //I really just don't care now.
            {
                return new Directory[0];
            }
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
            this.isAuthenticated = false;
            new ShokoServiceImplementation().SaveUser(_user);
        }
    }
}