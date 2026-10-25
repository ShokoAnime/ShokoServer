using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Web;
using Shoko.Abstractions.Web.Enums;
using Shoko.Abstractions.Web.Events;
using Shoko.Abstractions.Web.Services;
using Shoko.Server.API.FileProviders;
using Shoko.Server.Plugin;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

namespace Shoko.Server.Services;

/// <summary>
///   Picks the web UI the server serves and keeps its releases up to
///   date.
/// </summary>
public class WebUIService(
    ILogger<WebUIService> logger,
    ISettingsProvider settingsProvider,
    ConfigurationProvider<WebUIServiceSettings> configurationProvider,
    ConfigurationProvider<ServerSettings> serverSettingsProvider,
    IConfigurationService configurationService,
    IPluginManager pluginManager,
    ISystemService systemService,
    CoreWebUIProvider coreProvider
) : IWebUIService
{
    #region Fields

    /// <summary>
    ///   Paths no web UI may be mounted at or below.
    /// </summary>
    private static readonly string[] _reservedPaths = ["/api", "/plugin", "/signalr"];

    private readonly SemverVersionComparer _versionComparer = new();

    private readonly Lock _lock = new();

    private IReadOnlyList<WebUIProviderInfo>? _providerInfos;

    private ServingState? _state;

    private bool _watching;

    private readonly HashSet<string> _refusedMountPaths = new(StringComparer.Ordinal);

    #endregion

    #region Add Parts

    /// <summary>
    ///   Registers the web UIs the plugins export. A plugin whose web UI
    ///   cannot be read, or asks for a path it may not have, fails.
    /// </summary>
    /// <param name="providers">The exported providers, the server's own among them.</param>
    /// <param name="onFailure">Called for each plugin whose web UI failed.</param>
    public void AddParts(IEnumerable<IWebUIProvider> providers, Action<LocalPluginInfo, Exception> onFailure)
    {
        lock (_lock)
        {
            if (_providerInfos is not null)
                return;

            logger.LogInformation("Initializing service.");

            var providerInfos = new List<WebUIProviderInfo>();
            foreach (var provider in providers)
            {
                var pluginInfo = pluginManager.GetPluginInfo(provider.GetType().Assembly)!;
                try
                {
                    providerInfos.Add(CreateInfo(provider, pluginInfo));
                }
                catch (Exception ex) when (pluginInfo.ID != CorePlugin.StaticID)
                {
                    onFailure(pluginInfo, ex);
                }
            }

            if (!providerInfos.Any(info => info.Provider == coreProvider))
                providerInfos.Add(CreateInfo(coreProvider, pluginManager.GetPluginInfo(typeof(CorePlugin).Assembly)!));

            _providerInfos = providerInfos
                .OrderByDescending(info => info.Provider == coreProvider)
                .ThenBy(info => info.PluginInfo.LoadOrder)
                .ToList();
        }
    }

    private WebUIProviderInfo CreateInfo(IWebUIProvider provider, LocalPluginInfo pluginInfo)
    {
        var providerType = provider.GetType();
        var configurationType = providerType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWebUIProvider<>))
            ?.GetGenericArguments()[0];

        if (!TryNormalizeMountPath(provider.MountPath, GetReservedPaths(settingsProvider.GetSettings().Web), out var mountPath, out var reason))
            throw new InvalidOperationException($"The web UI \"{provider.Name}\" cannot be served from \"{provider.MountPath}\"; {reason}");

        return new()
        {
            ID = GetID(providerType, pluginInfo),
            Version = provider.Version,
            Name = provider.Name,
            Description = provider.Description?.CleanDescription() ?? string.Empty,
            MountPath = mountPath,
            Provider = provider,
            ConfigurationInfo = configurationType is null ? null : configurationService.GetConfigurationInfo(configurationType),
            PluginInfo = pluginInfo,
        };
    }

    #endregion

    #region Web UIs

    /// <summary>
    ///   What is served now: the active web UI, whether serving is on, and the
    ///   path it is mounted at, with the path its provider gave, to notice
    ///   when that changes.
    /// </summary>
    /// <param name="Active">The active web UI.</param>
    /// <param name="IsEnabled">Whether the active web UI is served.</param>
    /// <param name="MountPath">The active web UI's mount path.</param>
    /// <param name="ActiveProviderMountPath">The mount path the active provider gave.</param>
    internal sealed record ServingState(
        WebUIProviderInfo Active,
        bool IsEnabled,
        string MountPath,
        string? ActiveProviderMountPath
    );

    /// <inheritdoc />
    public bool IsEnabled
    {
        get => configurationProvider.Load().IsEnabled;
        set
        {
            var settings = configurationProvider.Load(copy: true);
            if (settings.IsEnabled == value)
                return;

            settings.IsEnabled = value;
            configurationProvider.Save(settings);
            Refresh();
        }
    }

    /// <inheritdoc />
    public WebUIProviderInfo ActiveProvider
        => GetState().Active;

    /// <inheritdoc />
    public void SelectProvider(WebUIProviderInfo? webUI)
    {
        var settings = configurationProvider.Load(copy: true);
        if (settings.SelectedProviderID == webUI?.ID)
            return;

        settings.SelectedProviderID = webUI?.ID;
        configurationProvider.Save(settings);
        Refresh();
    }

    /// <inheritdoc />
    public event EventHandler<WebUIActiveProviderChangedEventArgs>? ActiveProviderChanged;

    /// <summary>
    ///   Gets what is served now. Cheap enough for every request: it only
    ///   works it out again when a provider gives another mount path, the
    ///   active web UI's plugin failed, or after the selection or the settings
    ///   were saved.
    /// </summary>
    /// <returns>The serving state.</returns>
    internal ServingState GetState()
    {
        var state = _state;
        if (state is not null &&
            !state.Active.PluginInfo.HasFailed &&
            ReadMountPath(state.Active) == state.ActiveProviderMountPath)
            return state;

        return Refresh();
    }

    /// <summary>
    ///   Works out what is served now: the selected web UI if it is there and
    ///   did not fail, else the first plugin web UI, else the server's own.
    ///   A change of web UI moves the release events over and brings the new
    ///   one's files in line in the background.
    /// </summary>
    /// <returns>The serving state.</returns>
    private ServingState Refresh()
    {
        lock (_lock)
        {
            WatchSettings();

            var providerInfos = GetProviderInfosLocked();
            var core = providerInfos.First(info => info.Provider == coreProvider);
            var selectedID = configurationProvider.Load().SelectedProviderID;
            var usable = providerInfos.Where(info => !info.PluginInfo.HasFailed).ToList();
            var active = (selectedID is { } id ? usable.FirstOrDefault(info => info.ID == id) : null)
                ?? usable.FirstOrDefault(info => info.Provider != coreProvider)
                ?? core;

            foreach (var info in providerInfos)
            {
                info.IsActive = info == active;
                info.IsSelected = info.ID == selectedID;
            }

            var activeProviderMountPath = UpdateMountPath(active);
            var isEnabled = configurationProvider.Load().IsEnabled;
            var previous = _state;
            var state = new ServingState(active, isEnabled, active.MountPath, activeProviderMountPath);
            if (state == previous)
                return previous;

            _state = state;
            if (previous?.Active != active)
            {
                if (previous is not null)
                    previous.Active.Provider.ReleaseInstalled -= OnActiveProviderReleaseInstalled;
                active.Provider.ReleaseInstalled += OnActiveProviderReleaseInstalled;
                if (selectedID is { } missingID && active.ID != missingID)
                    logger.LogWarning("The selected web UI {ProviderID} is not available. Serving {Name} instead.", missingID, active.Name);
            }

            if (previous is null || previous.Active != active || previous.MountPath != state.MountPath || previous.IsEnabled != isEnabled)
            {
                if (isEnabled)
                    logger.LogInformation("Serving the web UI {Name} from {Path}.", active.Name, active.MountPath);
                else
                    logger.LogInformation("Not serving a web UI. {Name} is kept up to date.", active.Name);
            }

            if (previous is null)
                return state;

            // The new web UI may have nothing installed yet; the start-up rules apply to it now.
            if (previous.Active != active)
            {
                using (DetachedFlow.Suppress())
                    _ = Task.Run(ApplyStartPolicy);
            }

            if (previous.Active != active || previous.IsEnabled != isEnabled || previous.MountPath != state.MountPath)
            {
                var eventArgs = new WebUIActiveProviderChangedEventArgs { Provider = active, IsEnabled = isEnabled };
                _ = Task.Run(() => ActiveProviderChanged?.Invoke(this, eventArgs));
            }

            return state;
        }
    }

    /// <summary>
    ///   Starts following saves of the selection and of the server settings,
    ///   once.
    /// </summary>
    private void WatchSettings()
    {
        if (_watching)
            return;

        _watching = true;
        configurationProvider.Saved += (_, _) => Refresh();
        serverSettingsProvider.Saved += (_, _) => Refresh();
    }

    /// <summary>
    ///   Takes the provider's mount path into the web UI's info when it is
    ///   allowed, and keeps the last allowed one otherwise.
    /// </summary>
    /// <param name="info">The web UI.</param>
    /// <returns>The mount path the provider gave, or <c>null</c> if it could not be read.</returns>
    private string? UpdateMountPath(WebUIProviderInfo info)
    {
        var providerMountPath = ReadMountPath(info);
        if (providerMountPath is null)
            return null;

        if (TryNormalizeMountPath(providerMountPath, GetReservedPaths(settingsProvider.GetSettings().Web), out var mountPath, out var reason))
            info.MountPath = mountPath;
        else if (_refusedMountPaths.Add(providerMountPath))
            logger.LogWarning("The web UI {Name} cannot be served from {Path}; {Reason} Keeping {MountPath}.", info.Name, providerMountPath, reason, info.MountPath);
        return providerMountPath;
    }

    /// <summary>
    ///   Reads a provider's mount path, or <c>null</c> if it throws.
    /// </summary>
    private string? ReadMountPath(WebUIProviderInfo info)
    {
        try
        {
            return info.Provider.MountPath;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to read the mount path of the web UI {Name}.", info.Name);
            return null;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<WebUIProviderInfo> GetAvailableProviders()
    {
        // Worked out first, so their flags are current.
        GetState();
        lock (_lock)
            return GetProviderInfosLocked();
    }

    /// <inheritdoc />
    public WebUIProviderInfo? GetProviderInfo(Guid providerID)
        => GetAvailableProviders().FirstOrDefault(info => info.ID == providerID);

    /// <inheritdoc />
    public WebUIProviderInfo GetProviderInfo(IWebUIProvider provider)
        => GetAvailableProviders().FirstOrDefault(info => info.Provider == provider)
            ?? throw new ArgumentException($"The web UI provider \"{provider.GetType().FullName}\" is not registered with the service.", nameof(provider));

    /// <inheritdoc />
    public WebUIProviderInfo GetProviderInfo<TProvider>() where TProvider : class, IWebUIProvider
        => GetAvailableProviders().FirstOrDefault(info => info.Provider is TProvider)
            ?? throw new ArgumentException($"The web UI provider \"{typeof(TProvider).FullName}\" is not registered with the service.", nameof(TProvider));

    /// <summary>
    ///   The registered web UIs. Before the plugins handed in theirs, only the
    ///   server's own is known, and the list is kept that way.
    /// </summary>
    private IReadOnlyList<WebUIProviderInfo> GetProviderInfosLocked()
        => _providerInfos ??= [CreateInfo(coreProvider, pluginManager.GetPluginInfo(typeof(CorePlugin).Assembly)!)];

    #endregion

    #region Releases

    /// <inheritdoc />
    public event EventHandler<WebUIReleaseInstalledEventArgs>? ReleaseInstalled;

    private void OnActiveProviderReleaseInstalled(object? sender, EventArgs eventArgs)
    {
        var webUI = GetAvailableProviders().FirstOrDefault(info => info.Provider == sender) ?? ActiveProvider;
        ReleaseInstalled?.Invoke(this, new() { Provider = webUI, Release = webUI.Provider.CurrentRelease });
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WebUIReleaseInformation>> GetReleases(ReleaseChannel? channel = null, bool force = false, CancellationToken cancellationToken = default)
    {
        var webUI = ActiveProvider;
        if (channel is ReleaseChannel.Auto)
            channel = GetChannel(webUI);

        return (await webUI.Provider.GetReleases(force, cancellationToken))
            .Where(release => !channel.HasValue || release.Channel == channel.Value)
            .Select(WithServerCompatibility)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<WebUIReleaseInformation?> GetLatestRelease(
        ReleaseChannel channel = ReleaseChannel.Auto,
        bool force = false,
        bool allowIncompatible = false,
        CancellationToken cancellationToken = default
    )
    {
        var webUI = ActiveProvider;
        if (channel is ReleaseChannel.Auto)
            channel = GetChannel(webUI);

        var releases = await GetReleases(channel, force, cancellationToken);
        var nextIndex = releases.FindIndex(release => allowIncompatible || release.IncompatibleReason is null);
        if (nextIndex is -1)
            return null;

        var next = releases[nextIndex];
        var current = webUI.Provider.CurrentRelease;
        var currentIndex = current is null ? -1 : releases.FindIndex(release => release.Version == current.Version && release.Channel == current.Channel);
        if (currentIndex is -1)
            return next;

        // The notes of every release since the installed one, at most twenty.
        var releaseNotes = new StringBuilder();
        if (nextIndex == currentIndex)
        {
            releaseNotes.Append(releases[currentIndex].Description ?? "N/A");
        }
        else
        {
            var endIndex = Math.Min(currentIndex - 1, nextIndex + 19);
            for (var i = endIndex; i >= nextIndex; i--)
                releaseNotes
                    .AppendLine()
                    .AppendLine($"**{releases[i].Version}**:")
                    .AppendLine()
                    .AppendLine(releases[i].Description ?? "N/A");
            if (endIndex < currentIndex - 1)
                releaseNotes
                    .AppendLine()
                    .AppendLine("…");
        }

        var latest = Copy(next);
        latest.Description = releaseNotes.ToString().Trim();
        return latest;
    }

    /// <inheritdoc />
    public Task<WebUIInstallResult> InstallRelease(WebUIReleaseInformation release, CancellationToken cancellationToken = default)
        => ActiveProvider.Provider.InstallRelease(release, cancellationToken);

    /// <inheritdoc />
    public async Task<WebUIInstallResult> Update(ReleaseChannel channel = ReleaseChannel.Auto, bool allowIncompatible = false, CancellationToken cancellationToken = default)
    {
        if (await GetLatestRelease(channel, allowIncompatible: allowIncompatible, cancellationToken: cancellationToken) is not { } latest)
            return WebUIInstallResult.Failed;

        return await InstallRelease(latest, cancellationToken);
    }

    #endregion

    #region Start Policy

    /// <summary>
    ///   Installs the active webUI's bundled release before it is served
    ///   when nothing is installed, the installed release cannot be used, or
    ///   the bundled one is newer and the setting allows it. A debug build is
    ///   only replaced by a newer one. An unusable release without a usable
    ///   bundled one is replaced in the background.
    /// </summary>
    public void ApplyStartPolicy()
    {
        var webUI = ActiveProvider;
        var current = webUI.Provider.CurrentRelease;
        var bundled = webUI.Provider.BundledRelease is { } release ? WithServerCompatibility(release) : null;
        string? reason = null;
        if (bundled is { IncompatibleReason: null })
        {
            if (current is null)
                reason = "nothing is installed";
            else if (current.Channel is not ReleaseChannel.Debug && WithServerCompatibility(current).IncompatibleReason is { } incompatibleReason)
                reason = $"the installed release cannot be used: {incompatibleReason}";
            else if (settingsProvider.GetSettings().Web.AutoReplaceWebUIWithIncluded && IsNewerOnSameChannel(bundled, current))
                reason = "the bundled release is newer";
        }

        try
        {
            if (reason is not null)
            {
                logger.LogInformation("Installing the bundled release {Version} of {Name}, since {Reason}.", bundled!.Version.ToSemanticVersioningString(), webUI.Name, reason);
                var result = webUI.Provider.InstallRelease(webUI.Provider.BundledRelease!).GetAwaiter().GetResult();
                if (result is WebUIInstallResult.Failed)
                    logger.LogWarning("Failed to install the bundled release of {Name}.", webUI.Name);
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to install the bundled release of {Name}.", webUI.Name);
            return;
        }

        // A debug build is a local one, which is never replaced behind its developer's back.
        if (current is { Channel: not ReleaseChannel.Debug } && WithServerCompatibility(current).IncompatibleReason is { } stillIncompatible)
        {
            logger.LogWarning("The installed release of {Name} cannot be used: {Reason} Looking for one that can.", webUI.Name, stillIncompatible);
            using (DetachedFlow.Suppress())
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (await Update(current.Channel) is WebUIInstallResult.Failed)
                            logger.LogWarning("No release of {Name} that this server can use was found.", webUI.Name);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to replace the incompatible release of {Name}.", webUI.Name);
                    }
                });
        }
    }

    /// <summary>
    ///   Checks if a release is newer than the installed one on the same kind
    ///   of channel. A debug build only replaces a debug build.
    /// </summary>
    private bool IsNewerOnSameChannel(WebUIReleaseInformation candidate, WebUIReleaseInformation installed)
        => _versionComparer.Compare(candidate.Version, installed.Version) > 0 &&
            (candidate.Channel is ReleaseChannel.Debug) == (installed.Channel is ReleaseChannel.Debug);

    #endregion

    #region Helpers

    /// <summary>
    ///   The channel <see cref="ReleaseChannel.Auto"/> stands for: the
    ///   installed release's, else the server's, with a debug server on
    ///   stable.
    /// </summary>
    private ReleaseChannel GetChannel(WebUIProviderInfo webUI)
    {
        if (webUI.Provider.CurrentRelease is { } current)
            return current.Channel;

        return systemService.Version.Channel is ReleaseChannel.Debug ? ReleaseChannel.Stable : systemService.Version.Channel;
    }

    /// <summary>
    ///   A copy of the release, with the server's own reason added when the
    ///   running server is outside the release's server versions.
    /// </summary>
    private WebUIReleaseInformation WithServerCompatibility(WebUIReleaseInformation release)
    {
        var copy = Copy(release);
        if (copy.IncompatibleReason is not null)
            return copy;

        var serverVersion = Assembly.GetExecutingAssembly().GetName().Version!;
        if (copy.MinimumServerVersion is { } minimum && _versionComparer.Compare(minimum, serverVersion) > 0)
            copy.IncompatibleReason = $"It needs server version {minimum.ToSemanticVersioningString()} or newer.";
        else if (copy.MaximumServerVersion is { } maximum && _versionComparer.Compare(maximum, serverVersion) < 0)
            copy.IncompatibleReason = $"It needs server version {maximum.ToSemanticVersioningString()} or older.";
        return copy;
    }

    private static WebUIReleaseInformation Copy(WebUIReleaseInformation release)
        => new()
        {
            Version = release.Version,
            Description = release.Description,
            SourceRevision = release.SourceRevision,
            ReleaseTag = release.ReleaseTag,
            Channel = release.Channel,
            ReleasedAt = release.ReleasedAt,
            MinimumServerVersion = release.MinimumServerVersion,
            MaximumServerVersion = release.MaximumServerVersion,
            IsBundled = release.IsBundled,
            IncompatibleReason = release.IncompatibleReason,
        };

    /// <summary>
    ///   The paths no webUI may be mounted at or below: the reserved ones and
    ///   the Swagger UI's.
    /// </summary>
    /// <param name="webSettings">The web settings, for the Swagger UI's path.</param>
    /// <returns>The reserved paths.</returns>
    internal static string[] GetReservedPaths(WebSettings webSettings)
        => [.. _reservedPaths, "/" + webSettings.SwaggerUIPrefix.Trim('/')];

    /// <summary>
    ///   Normalizes a mount path to a leading slash and no trailing one, with
    ///   <c>/</c> for the root, and checks it is allowed.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <param name="reservedPaths">The paths it may not be at or below.</param>
    /// <param name="normalized">The normalized path, or <c>/</c> when refused.</param>
    /// <param name="reason">Why the path is refused, or <c>null</c>.</param>
    /// <returns><c>true</c> if the path is allowed.</returns>
    internal static bool TryNormalizeMountPath(string? path, IReadOnlyList<string> reservedPaths, out string normalized, out string? reason)
    {
        normalized = "/";
        reason = null;
        var trimmed = (path ?? string.Empty).Trim().Trim('/');
        if (trimmed.Length is 0)
            return true;

        var segments = trimmed.Split('/');
        if (segments.Any(segment => segment is "" or "." or ".." || segment.IndexOfAny(['?', '#', '\\', '%']) >= 0))
        {
            reason = "the path is not a plain path.";
            return false;
        }

        var candidate = "/" + trimmed;
        foreach (var reservedPath in reservedPaths)
        {
            if (reservedPath is "/" or "")
                continue;

            if (candidate.Equals(reservedPath, StringComparison.OrdinalIgnoreCase) || candidate.StartsWith(reservedPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                reason = $"the path is under {reservedPath}, which is reserved.";
                return false;
            }
        }

        normalized = candidate;
        return true;
    }

    /// <summary>
    ///   The ID of a web UI provider, from its type's full name and the ID
    ///   of the plugin it ships in.
    /// </summary>
    private static Guid GetID(Type providerType, LocalPluginInfo pluginInfo)
        => UuidUtility.GetV5($"WebUIProvider={providerType.FullName!}", pluginInfo.ID);

    #endregion
}
