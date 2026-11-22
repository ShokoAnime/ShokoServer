using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Core;
using Shoko.Server.Settings;

namespace Shoko.Server.API;

/// <summary>
///   The core APIs this server serves, settled once while it starts: after
///   the web UIs are known, so one in use gets the APIs it needs, and before
///   the routes are built.
/// </summary>
/// <remarks>
///   Process-wide, as the routes and the API documents are set up before the
///   service container exists.
/// </remarks>
internal sealed class ServedApis
{
    /// <summary>
    ///   The server's.
    /// </summary>
    public static ServedApis Instance { get; } = new();

    private readonly object _lock = new();

    private CoreApis? _running;

    /// <summary>
    ///   The APIs served. Read before they are settled, they are what the
    ///   settings say, without settling them.
    /// </summary>
    public CoreApis Running
    {
        get
        {
            lock (_lock)
                return _running ?? FromSettings(ISettingsProvider.Instance.GetSettings().Web);
        }
    }

    /// <summary>
    ///   Whether the APIs served are settled. They are, once the web UIs are
    ///   known and before the web host starts.
    /// </summary>
    public bool IsSettled
    {
        get
        {
            lock (_lock)
                return _running is not null;
        }
    }

    /// <summary>
    ///   Settles the APIs served, unless they were settled already.
    /// </summary>
    /// <param name="apis">The APIs.</param>
    /// <returns><c>true</c> if they were settled now.</returns>
    public bool Settle(CoreApis apis)
    {
        lock (_lock)
        {
            if (_running is not null)
                return false;
            _running = apis;
            return true;
        }
    }

    /// <summary>
    ///   Checks if the API is served.
    /// </summary>
    /// <param name="api">The API.</param>
    /// <returns><c>true</c> if it is.</returns>
    public bool Serves(CoreApis api)
        => (Running & api) == api;

    /// <summary>
    ///   The APIs the web settings turn on.
    /// </summary>
    /// <param name="web">The web settings.</param>
    /// <returns>The APIs.</returns>
    public static CoreApis FromSettings(WebSettings web)
        => (web.EnableAPIv2 ? CoreApis.V2 : CoreApis.None) | (web.EnableAPIv3 ? CoreApis.V3 : CoreApis.None);

    /// <summary>
    ///   The environment variable that sets each API.
    /// </summary>
    public static readonly IReadOnlyDictionary<CoreApis, string> EnvironmentVariables = new Dictionary<CoreApis, string>
    {
        [CoreApis.V2] = "SHOKO_API_V2_ENABLED",
        [CoreApis.V3] = "SHOKO_API_V3_ENABLED",
    };

    /// <summary>
    ///   The APIs an environment variable sets, on or off.
    /// </summary>
    /// <returns>The APIs.</returns>
    public static CoreApis SetByEnvironment()
        => EnvironmentVariables.Where(pair => Environment.GetEnvironmentVariable(pair.Value) is not null).Aggregate(CoreApis.None, (apis, pair) => apis | pair.Key);

    /// <summary>
    ///   The APIs an environment variable keeps off.
    /// </summary>
    /// <param name="web">The web settings, with the environment applied.</param>
    /// <returns>The APIs.</returns>
    public static CoreApis KeptOffByEnvironment(WebSettings web)
        => SetByEnvironment() & ~FromSettings(web);

    /// <summary>
    ///   Names APIs for a message, such as "APIv3" or "APIv2 and APIv3".
    /// </summary>
    /// <param name="apis">The APIs.</param>
    /// <returns>Their names.</returns>
    public static string Describe(CoreApis apis)
        => string.Join(" and ", EnvironmentVariables.Keys.Where(api => apis.HasFlag(api)).Select(api => $"API{api.ToString().ToLowerInvariant()}"));
}
