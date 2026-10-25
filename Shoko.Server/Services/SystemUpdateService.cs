using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;

using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

namespace Shoko.Server.Services;

public class SystemUpdateService(
    ISettingsProvider settingsProvider,
    ISystemService systemService,
    ReleaseManifestReader manifestReader
) : ISystemUpdateService
{
    #region Server

    /// <inheritdoc />
    public string ServerManifestUrl
    {
        get => settingsProvider.GetSettings().Web.ServerManifestUrl;
        set
        {
            // If validation fails then catch and swallow the exception.
            try
            {
                var copy = settingsProvider.GetSettings(true);
                copy.Web.ServerManifestUrl = value;
                settingsProvider.SaveSettings(copy);
            }
            catch { }
        }
    }

    /// <inheritdoc />
    public async Task<ReleaseVersionInformation?> GetLatestServerVersion(ReleaseChannel channel = ReleaseChannel.Auto, bool force = false)
    {
        if (channel is ReleaseChannel.Auto)
            channel = GetCurrentServerReleaseChannel();

        var versions = await GetServerHistory(channel, force);
        if (versions.Count is 0)
            return null;

        // The latest version is at index 0 (sorted descending).
        var latestVersion = versions[0];
        var currentServerVersion = Assembly.GetExecutingAssembly().GetName().Version!;
        var currentVersionIndex = versions.FindIndex(v => v.Version == currentServerVersion);
        if (currentVersionIndex is <= 0)
            return latestVersion;

        var endIndex = Math.Min(currentVersionIndex - 1, 19);
        var releaseNotes = new StringBuilder();
        for (var i = endIndex; i >= 0; i--)
            releaseNotes
                .AppendLine()
                .AppendLine($"**{versions[i].Version}**:")
                .AppendLine()
                .AppendLine(versions[i].Description ?? "N/A");
        if (endIndex < currentVersionIndex - 1)
            releaseNotes
                .AppendLine()
                .AppendLine("…");

        return new()
        {
            Version = latestVersion.Version,
            Description = releaseNotes.ToString().Trim(),
            SourceRevision = latestVersion.SourceRevision,
            ReleaseTag = latestVersion.ReleaseTag,
            Channel = latestVersion.Channel,
            ReleasedAt = latestVersion.ReleasedAt,
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReleaseVersionInformation>> GetServerHistory(ReleaseChannel? channel = null, bool force = false)
    {
        if (channel is ReleaseChannel.Auto)
            channel = GetCurrentServerReleaseChannel();

        return (await manifestReader.FetchManifestAsync(ServerManifestUrl, "server-manifest.json", force))
            .Where(tuple => !channel.HasValue || tuple.Channel == channel.Value)
            .Select(tuple => ReleaseManifestReader.ToReleaseInfo(tuple.Entry, tuple.Channel))
            .ToList();
    }

    #region Server | Internals

    private ReleaseChannel GetCurrentServerReleaseChannel()
    {
        if (systemService.Version.Channel is ReleaseChannel.Debug)
            return ReleaseChannel.Stable;
        return systemService.Version.Channel;
    }

    #endregion

    #endregion
}
