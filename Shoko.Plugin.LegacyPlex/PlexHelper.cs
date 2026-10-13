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
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Video.Services;
using Shoko.Plugin.LegacyPlex.Models;
using Shoko.Plugin.LegacyPlex.Models.Connections;
using Shoko.Plugin.LegacyPlex.Models.Login;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;

using Directory = Shoko.Plugin.LegacyPlex.Models.Libraries.Directory;
using MediaContainer = Shoko.Plugin.LegacyPlex.Models.Connections.MediaContainer;

namespace Shoko.Plugin.LegacyPlex;

/// <summary>
///   A user's Plex client: links their Plex account, lists the servers and
///   libraries it reaches, and asks the picked server.
/// </summary>
public class PlexHelper
{
    /// <summary>
    ///   The name of the HTTP client Plex is asked with.
    /// </summary>
    internal const string HttpClientName = "LegacyPlex";

    private const string ClientIdentifier = "d14f0724-a4e8-498a-bb67-add795b38331";
    private const string PlexAccountURI = "https://plex.tv/users/account.json";

    private readonly ILogger<PlexHelper> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISettingsProvider _settingsProvider;
    private readonly JMMUserRepository _jmmUsers;

    private readonly int _userId;
    private JMMUser? _user => _jmmUsers.GetByID(_userId);

    private readonly string _version;

    internal readonly JsonSerializerSettings SerializerSettings = new();

    /// <summary>
    ///   Finds the files Plex plays, for the episodes read through this client.
    /// </summary>
    internal IVideoService VideoService { get; }

    private Connection? _cachedConnection;
    private PlexKey? _key;
    private DateTime _lastCacheTime = DateTime.MinValue;
    private DateTime _lastMediaCacheTime = DateTime.MinValue;

    private MediaDevice[]? _plexMediaDevices;

    private MediaDevice? _mediaDevice;

    internal PlexHelper(
        int userID,
        ILogger<PlexHelper> logger,
        IHttpClientFactory httpClientFactory,
        ISettingsProvider settingsProvider,
        JMMUserRepository jmmUsers,
        ISystemService systemService,
        IVideoService videoService
    )
    {
        _userId = userID;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _settingsProvider = settingsProvider;
        _jmmUsers = jmmUsers;
        _version = systemService.Version.Version.ToSemanticVersioningString();
        VideoService = videoService;
        SerializerSettings.Converters.Add(new PlexConverter(this));
    }

    public MediaDevice ServerCache
    {
        get
        {
            var settings = _settingsProvider.GetSettings().Plex;
            if (string.IsNullOrEmpty(settings.Server))
            {
                return null!;
            }

            if (DateTime.Now - TimeSpan.FromHours(1) >= _lastMediaCacheTime)
            {
                _mediaDevice = null;
            }

            if (_mediaDevice != null && settings.Server == _mediaDevice.ClientIdentifier)
            {
                return _mediaDevice;
            }

            _mediaDevice = GetPlexServers()
                .FirstOrDefault(s => s.ClientIdentifier == settings.Server);
            if (_mediaDevice != null)
            {
                _lastMediaCacheTime = DateTime.Now;
                return _mediaDevice;
            }

            if (!settings.Server.Contains(':'))
            {
                return null!;
            }


            var strings = settings.Server.Split(':');
            _mediaDevice = GetPlexServers().FirstOrDefault(s =>
                s.Connection!.Any(c => c.Address == strings[0] && c.Port == strings[1]));
            if (_mediaDevice != null)
            {
                settings.Server = _mediaDevice.ClientIdentifier!;
                _settingsProvider.SaveSettings();
            }

            _lastMediaCacheTime = DateTime.Now;
            return _mediaDevice!;
        }
        private set
        {
            _mediaDevice = value;
            _cachedConnection = null;
            _lastMediaCacheTime = DateTime.Now;
        }
    }

    private Connection? ConnectionCache
    {
        get
        {
            if (DateTime.Now - TimeSpan.FromHours(12) < _lastCacheTime && _cachedConnection != null)
            {
                return _cachedConnection;
            }

            _cachedConnection = null;

            //foreach (var connection in ServerCache.Connection)
            Parallel.ForEach(ServerCache.Connection!, (connection, state) =>
                {
                    try
                    {
                        if (state.ShouldExitCurrentIteration)
                        {
                            return;
                        }

                        var (result, _) = RequestAsync($"{connection.Uri}/library/sections", HttpMethod.Get,
                                new Dictionary<string, string> { { "X-Plex-Token", ServerCache.AccessToken! } })
                            .Result;

                        if (result != HttpStatusCode.OK)
                        {
                            _logger.LogTrace("Got response from: {Uri} {Result}", connection.Uri, result);
                            return;
                        }

                        _cachedConnection = _cachedConnection ?? connection;
                        state.Break();
                    }
                    catch (AggregateException e)
                    {
                        _logger.LogTrace(e, "Failed connection to: {Uri}", connection.Uri);
                    }
                }
            );

            _lastCacheTime = DateTime.Now;

            return _cachedConnection;
        }
    }

    private Dictionary<string, string> AuthenticationHeaders => new() { { "X-Plex-Token", GetPlexToken()! } };

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
                // Without a token there is nothing to check, and a pin still waiting must be kept.
                if (GetPlexToken() is not { Length: > 0 } token)
                    return false;

                var (status, _) = RequestAsync(PlexAccountURI, HttpMethod.Get,
                    new Dictionary<string, string> { { "X-Plex-Token", token } }).ConfigureAwait(false).GetAwaiter().GetResult();

                if (status == HttpStatusCode.OK)
                {
                    _lastAuthenticated = DateTime.Now;
                    return true;
                }

                if (status == HttpStatusCode.UnprocessableEntity)
                {
                    _logger.LogWarning("UnprocessableEntity returned when authenticating Plex user. Invalidating token.");
                    InvalidateToken();
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Exception during Plex authentication");
                return false;
            }
        }
    }

    public string LoginUrl => $"https://app.plex.tv/auth#?clientID={ClientIdentifier}" +
                              $"&code={GetPlexKey()!.Code}"; /* +
                                  "&context%5Bdevice%5D%5Bproduct%5D=Shoko%20Server" +
                                  $"&context%5Bdevice%5D%5Bplatform%5D={WebUtility.UrlEncode(Environment.OSVersion.Platform.ToString())}" +
                                  $"&context%5Bdevice%5D%5BplatformVersion%5D={WebUtility.UrlEncode(Environment.OSVersion.VersionString)}" +
                                  $"&context%5Bdevice%5D%5Bversion%5D={WebUtility.UrlEncode(Assembly.GetEntryAssembly().GetName().Version.ToString())}";*/

    private PlexKey? GetPlexKey()
    {
        if (_key != null && _key.ExpiresAt > DateTime.Now) return _key;

        var (_, content) = RequestAsync("https://plex.tv/api/v2/pins?strong=true", HttpMethod.Post).Result;
        return _key = JsonConvert.DeserializeObject<PlexKey>(content);
    }

    private string? GetPlexToken()
    {
        var user = _user;
        if (user?.PlexToken is { Length: > 0 } token) return token;

        // Only the login URL starts a pin; without one there is no token to wait for.
        if (_key == null) return null;
        if (_key.AuthToken != null) return _key.AuthToken;

        string? content = null;
        try
        {
            (_, content) = RequestAsync($"https://plex.tv/api/v2/pins/{_key.Id}", HttpMethod.Get).Result;
            _key = JsonConvert.DeserializeObject<PlexKey>(content);
        }
        catch
        {
            _logger.LogTrace("Unable to deserialize Plex Key from server. Response was \n{Content}", content);
        }

        if (string.IsNullOrEmpty(_key?.AuthToken)) return null;

        if (user == null)
        {
            _logger.LogError("Could not get the user in {Method}. The plex token will not persist", nameof(GetPlexToken));
            return _key.AuthToken;
        }

        SaveToken(user, _key.AuthToken);
        return _key.AuthToken;
    }

    private void SaveToken(JMMUser user, string? token)
    {
        user.PlexToken = token;
        _jmmUsers.Save(user);
    }

    private async Task<(HttpStatusCode status, string content)> RequestAsync(string url, HttpMethod method,
        IDictionary<string, string>? headers = default, string? content = null, bool xml = false,
        Action<HttpRequestMessage>? configureRequest = null)
    {
        //headers["Accept"] = xml ? "application/xml" : "application/json";
        _logger.LogTrace("Requesting from plex: {Method} {Url}", method.Method, url);

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
        req.Headers.Add("X-Plex-Client-Identifier", ClientIdentifier);
        req.Headers.Add("X-Plex-Platform-Version", _version);
        req.Headers.Add("X-Plex-Platform", "Shoko Server");
        req.Headers.Add("X-Plex-Device-Name", "Shoko Server Sync");
        req.Headers.Add("X-Plex-Product", "Shoko Server Sync");
        req.Headers.Add("X-Plex-Device", "Shoko");
        req.Headers.TryAddWithoutValidation("User-Agent", $"{assemblyName?.Name} v{assemblyName?.Version}");

        using var client = _httpClientFactory.CreateClient(HttpClientName);
        using var resp = await client.SendAsync(req).ConfigureAwait(false);
        _logger.LogTrace("Got response: {StatusCode}", resp.StatusCode);
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
            _logger.LogTrace("Unable to deserialize Plex Devices from server. Response was \n{Content}", content);
            return Array.Empty<MediaDevice>();
        }
    }

    public List<MediaDevice> GetPlexServers()
    {
        if (!IsAuthenticated)
        {
            return new List<MediaDevice>();
        }

        return GetPlexDevices().Where(d => d.Provides!.Split(',').Contains("server")).ToList();
    }

    public void UseServer(MediaDevice server)
    {
        var settings = _settingsProvider.GetSettings().Plex;
        if (server == null)
        {
            settings.Server = string.Empty;
            _settingsProvider.SaveSettings();
            return;
        }

        if (!server.Provides!.Split(',').Contains("server"))
        {
            return; //not allowed.
        }

        settings.Server = server.ClientIdentifier!;
        ServerCache = server;
        _settingsProvider.SaveSettings();
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
                .DeserializeObject<MediaContainer<Models.Libraries.MediaContainer>>(data, SerializerSettings)!
                .Container.Directory ?? Array.Empty<Directory>();
        }
        catch (Exception) //I really just don't care now.
        {
            return Array.Empty<Directory>();
        }
    }

    public Task<(HttpStatusCode status, string content)> RequestFromPlexAsync(string path,
        HttpMethod? method = null)
    {
        return RequestAsync($"{ConnectionCache!.Uri}{path}", method ?? HttpMethod.Get,
            new Dictionary<string, string> { { "X-Plex-Token", ServerCache.AccessToken! } });
    }

    public void InvalidateToken()
    {
        _lastAuthenticated = null;
        _key = null;
        if (_user is { } user && !string.IsNullOrEmpty(user.PlexToken))
            SaveToken(user, string.Empty);
    }
}
