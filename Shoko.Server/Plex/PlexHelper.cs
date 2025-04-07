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
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NLog;
using Quartz;
using Shoko.Models.Plex;
using Shoko.Models.Plex.Connections;
using Shoko.Models.Plex.Login;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using Directory = Shoko.Models.Plex.Libraries.Directory;
using MediaContainer = Shoko.Models.Plex.Connections.MediaContainer;

namespace Shoko.Server.Plex;

public class PlexHelper
{
    private const string ClientIdentifier = "d14f0724-a4e8-498a-bb67-add795b38331";

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly ConcurrentDictionary<int, PlexHelper> Cache = new();

    private readonly int _userId;
    private JMMUser _user => RepoFactory.JMMUser.GetByID(_userId);

    internal readonly JsonSerializerSettings SerializerSettings = new();

    private Connection _cachedConnection;
    private PlexKey _key;
    private DateTime _lastCacheTime = DateTime.MinValue;
    private DateTime _lastMediaCacheTime = DateTime.MinValue;

    private MediaDevice[] _plexMediaDevices;

    private MediaDevice _mediaDevice;
    private bool? isAuthenticated;

    private PlexHelper(JMMUser user)
    {
        _userId = user.JMMUserID;
        SerializerSettings.Converters.Add(new PlexConverter(this));
    }

    public MediaDevice ServerCache
    {
        get
        {
            var settings = Utils.SettingsProvider.GetSettings();
            if (string.IsNullOrEmpty(settings.Plex.Server))
            {
                return null;
            }

            if (DateTime.Now - TimeSpan.FromHours(1) >= _lastMediaCacheTime)
            {
                _mediaDevice = null;
            }

            if (_mediaDevice != null && settings.Plex.Server == _mediaDevice.ClientIdentifier)
            {
                return _mediaDevice;
            }

            _mediaDevice = GetPlexServers()
                .FirstOrDefault(s => s.ClientIdentifier == settings.Plex.Server);
            if (_mediaDevice != null)
            {
                _lastMediaCacheTime = DateTime.Now;
                return _mediaDevice;
            }

            if (!settings.Plex.Server.Contains(':'))
            {
                return null;
            }


            var strings = settings.Plex.Server.Split(':');
            _mediaDevice = GetPlexServers().FirstOrDefault(s =>
                s.Connection.Any(c => c.Address == strings[0] && c.Port == strings[1]));
            if (_mediaDevice != null)
            {
                settings.Plex.Server = _mediaDevice.ClientIdentifier;
            }

            _lastMediaCacheTime = DateTime.Now;
            return _mediaDevice;
        }
        private set
        {
            _mediaDevice = value;
            _cachedConnection = null;
            _lastMediaCacheTime = DateTime.Now;
        }
    }

    private Connection ConnectionCache
    {
        get
        {
            if (DateTime.Now - TimeSpan.FromHours(12) < _lastCacheTime && _cachedConnection != null)
            {
                return _cachedConnection;
            }

            _cachedConnection = null;

            //foreach (var connection in ServerCache.Connection)
            Parallel.ForEach(ServerCache.Connection, (connection, state) =>
                {
                    try
                    {
                        if (state.ShouldExitCurrentIteration)
                        {
                            return;
                        }

                        var (result, _) = RequestAsync($"{connection.Uri}/library/sections", HttpMethod.Get,
                                new Dictionary<string, string> { { "X-Plex-Token", ServerCache.AccessToken } })
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

    private Dictionary<string, string> AuthenticationHeaders => new() { { "X-Plex-Token", GetPlexToken() } };

    private DateTime? _lastAuthenticated = null;

    public bool IsAuthenticated
    {
        get
        {
            if (_lastAuthenticated is not null && DateTime.Now - _lastAuthenticated < TimeSpan.FromMinutes(30))
            {
                return true;
            }

            try
            {
                var (status, _) = RequestAsync("https://plex.tv/users/account.json", HttpMethod.Get,
                    AuthenticationHeaders).ConfigureAwait(false).GetAwaiter().GetResult();

                if (status == HttpStatusCode.OK)
                {
                    isAuthenticated = true;
                    _lastAuthenticated = DateTime.Now;
                    return true;
                }

                if (status == HttpStatusCode.UnprocessableEntity)
                {
                    Logger.Warn("UnprocessableEntity returned when authenticating Plex user. Invalidating token.");
                    InvalidateToken();
                }

                isAuthenticated = false;
                return false;
            }
            catch (Exception ex)
            {
                Logger.Trace($"Exception during Plex authentication: {ex}");
                return false;
            }
        }
    }

    public string LoginUrl => $"https://app.plex.tv/auth#?clientID={ClientIdentifier}" +
                              $"&code={GetPlexKey().Code}"; /* +
                                  "&context%5Bdevice%5D%5Bproduct%5D=Shoko%20Server" +
                                  $"&context%5Bdevice%5D%5Bplatform%5D={WebUtility.UrlEncode(Environment.OSVersion.Platform.ToString())}" +
                                  $"&context%5Bdevice%5D%5BplatformVersion%5D={WebUtility.UrlEncode(Environment.OSVersion.VersionString)}" +
                                  $"&context%5Bdevice%5D%5Bversion%5D={WebUtility.UrlEncode(Assembly.GetEntryAssembly().GetName().Version.ToString())}";*/

    private PlexKey GetPlexKey()
    {
        if (_key != null && _key.ExpiresAt > DateTime.Now) return _key;

        var (_, content) = RequestAsync("https://plex.tv/api/v2/pins?strong=true", HttpMethod.Post).Result;
        return _key = JsonConvert.DeserializeObject<PlexKey>(content);
    }

    private string GetPlexToken()
    {
        if (!string.IsNullOrEmpty(_user?.PlexToken)) return _user.PlexToken;

        _key ??= GetPlexKey();
        if (_key == null) return null;
        if (_key.AuthToken != null) return _key.AuthToken;

        string content = null;
        try
        {
            (_, content) = RequestAsync($"https://plex.tv/api/v2/pins/{_key.Id}", HttpMethod.Get).Result;
            _key = JsonConvert.DeserializeObject<PlexKey>(content);
        }
        catch
        {
            Logger.Trace($"Unable to deserialize Plex Key from server. Response was \n{content}");
        }

        if (_key == null) return null;

        if (_user == null)
        {
            Logger.Error(@$"Could not get cached user in {nameof(GetPlexToken)}. The plex token will not persist");
            return _key.AuthToken;
        }

        _user.PlexToken = _key.AuthToken;
        SaveUser(_user);
        return _user.PlexToken;
    }

    private void SaveUser(JMMUser user)
    {
        try
        {
            var scheduler = Utils.ServiceContainer.GetRequiredService<ISchedulerFactory>().GetScheduler().GetAwaiter().GetResult();
            var existingUser = false;
            var updateStats = false;
            SVR_JMMUser jmmUser;
            if (user.JMMUserID != 0)
            {
                jmmUser = RepoFactory.JMMUser.GetByID(user.JMMUserID);
                if (jmmUser == null)
                {
                    return;
                }

                existingUser = true;
            }
            else
            {
                jmmUser = new SVR_JMMUser();
                updateStats = true;
            }

            if (existingUser && jmmUser.IsAniDBUser != user.IsAniDBUser)
            {
                updateStats = true;
            }

            var hcat = string.Join(",", user.HideCategories);

            jmmUser.HideCategories = hcat;
            jmmUser.IsAniDBUser = user.IsAniDBUser;
            jmmUser.IsTraktUser = user.IsTraktUser;
            jmmUser.IsAdmin = user.IsAdmin;
            jmmUser.Username = user.Username;
            jmmUser.CanEditServerSettings = user.CanEditServerSettings;
            jmmUser.PlexUsers = user.PlexUsers;
            jmmUser.PlexToken = user.PlexToken;
            if (string.IsNullOrEmpty(user.Password))
            {
                jmmUser.Password = string.Empty;
            }
            else
            {
                // Additional check for hashed password, if not hashed we hash it
                jmmUser.Password = user.Password.Length < 64 ? Digest.Hash(user.Password) : user.Password;
            }

            // make sure that at least one user is an admin
            if (jmmUser.IsAdmin == 0)
            {
                var adminExists = false;
                var users = RepoFactory.JMMUser.GetAll();
                foreach (var userOld in users)
                {
                    if (userOld.IsAdmin != 1)
                    {
                        continue;
                    }

                    if (existingUser)
                    {
                        if (userOld.JMMUserID != jmmUser.JMMUserID)
                        {
                            adminExists = true;
                        }
                    }
                    else
                    {
                        //one admin account is needed
                        adminExists = true;
                        break;
                    }
                }

                if (!adminExists)
                {
                    return;
                }
            }

            RepoFactory.JMMUser.Save(jmmUser);

            // update stats
            if (!updateStats)
            {
                return;
            }

            Task.WhenAll(RepoFactory.AnimeSeries.GetAll().Select(ser => scheduler.StartJob<RefreshAnimeStatsJob>(a => a.AnimeID = ser.AniDB_ID))).GetAwaiter()
                .GetResult();
        }
        catch
        {
            // ignore
        }
    }

    public static PlexHelper GetForUser(JMMUser user)
    {
        return Cache.GetOrAdd(user.JMMUserID, _ => new PlexHelper(user));
    }

    private async Task<(HttpStatusCode status, string content)> RequestAsync(string url, HttpMethod method,
        IDictionary<string, string> headers = default, string content = null, bool xml = false,
        Action<HttpRequestMessage> configureRequest = null)
    {
        //headers["Accept"] = xml ? "application/xml" : "application/json";
        Logger.Trace($"Requesting from plex: {method.Method} {url}");

        var req = new HttpRequestMessage(method, url);
        if (method == HttpMethod.Post)
        {
            req.Content = new StringContent(content ?? "");
        }

        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(xml ? "application/xml" : "application/json"));
        if (headers != null)
        {
            foreach (var (header, val) in headers)
            {
                if (req.Headers.Contains(header))
                {
                    req.Headers.Remove(header);
                }

                req.Headers.Add(header, val);
            }
        }

        configureRequest?.Invoke(req);

        var assemblyName = Assembly.GetEntryAssembly()?.GetName();
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("X-Plex-Client-Identifier", ClientIdentifier);
        client.DefaultRequestHeaders.Add("X-Plex-Platform-Version", ServerState.Instance.ApplicationVersion);
        client.DefaultRequestHeaders.Add("X-Plex-Platform", "Shoko Server");
        client.DefaultRequestHeaders.Add("X-Plex-Device-Name", "Shoko Server Sync");
        client.DefaultRequestHeaders.Add("X-Plex-Product", "Shoko Server Sync");
        client.DefaultRequestHeaders.Add("X-Plex-Device", "Shoko");
        client.DefaultRequestHeaders.Add("User-Agent", $"{assemblyName?.Name} v${assemblyName?.Version}");
        client.Timeout = TimeSpan.FromSeconds(10);

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
            _plexMediaDevices = Array.Empty<MediaDevice>();
            return _plexMediaDevices;
        }

        var (_, content) = RequestAsync("https://plex.tv/api/resources?includeHttps=1", HttpMethod.Get,
            AuthenticationHeaders).Result;
        var serializer = new XmlSerializer(typeof(MediaContainer));
        using TextReader reader = new StringReader(content);
        try
        {
            return ((MediaContainer)serializer.Deserialize(reader)!).Device ?? Array.Empty<MediaDevice>();
        }
        catch
        {
            Logger.Trace($"Unable to deserialize Plex Devices from server. Response was \n{reader}");
            return Array.Empty<MediaDevice>();
        }
    }

    public List<MediaDevice> GetPlexServers()
    {
        if (!IsAuthenticated)
        {
            return new List<MediaDevice>();
        }

        return GetPlexDevices().Where(d => d.Provides.Split(',').Contains("server")).ToList();
    }

    public void UseServer(MediaDevice server)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        if (server == null)
        {
            settings.Plex.Server = null;
            return;
        }

        if (!server.Provides.Split(',').Contains("server"))
        {
            return; //not allowed.
        }

        settings.Plex.Server = server.ClientIdentifier;
        ServerCache = server;
        Utils.SettingsProvider.SaveSettings();
    }

    public Directory[] GetDirectories()
    {
        if (ServerCache == null)
        {
            return Array.Empty<Directory>();
        }

        try
        {
            var (_, data) = RequestFromPlexAsync("/library/sections").Result;
            return JsonConvert
                .DeserializeObject<MediaContainer<Shoko.Models.Plex.Libraries.MediaContainer>>(data, SerializerSettings)
                .Container.Directory ?? Array.Empty<Directory>();
        }
        catch (Exception) //I really just don't care now.
        {
            return Array.Empty<Directory>();
        }
    }

    public Task<(HttpStatusCode status, string content)> RequestFromPlexAsync(string path,
        HttpMethod method = null)
    {
        return RequestAsync($"{ConnectionCache.Uri}{path}", method ?? HttpMethod.Get,
            new Dictionary<string, string> { { "X-Plex-Token", ServerCache.AccessToken } });
    }

    public void InvalidateToken()
    {
        _user.PlexToken = string.Empty;
        isAuthenticated = false;
        _lastAuthenticated = null;
        _key = null;
        SaveUser(_user);
    }
}
