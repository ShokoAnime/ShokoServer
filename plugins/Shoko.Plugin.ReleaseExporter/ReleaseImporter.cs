using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Release;

namespace Shoko.Plugin.ReleaseExporter;

/// <summary>
/// Responsible for importing releases from the file system near the video files.
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="applicationPaths">Application paths.</param>
/// <param name="configurationProvider">Configuration provider.</param>
public class ReleaseImporter(ILogger<ReleaseImporter> logger, IApplicationPaths applicationPaths, ConfigurationProvider<Configuration> configurationProvider) : IReleaseInfoProvider<Configuration>
{
    /// <inheritdoc/>
    public const string Key = "Release Importer";

    /// <inheritdoc/>
    public string Name { get; private set; } = Key;

    /// <inheritdoc/>
    public string Description { get; private set; } = """
        Responsible for importing releases from the file system near the video files.
    """;

    /// <inheritdoc/>
    public Task<ReleaseInfo?> GetReleaseInfoById(string releaseId, CancellationToken cancellationToken)
        => Task.FromResult<ReleaseInfo?>(null);

    /// <inheritdoc/>
    public async Task<ReleaseInfo?> GetReleaseInfoForVideo(ReleaseInfoRequest request, CancellationToken cancellationToken)
    {
        var (video, _) = request;
        logger.LogTrace("Trying to find release for video. (Video={VideoID})", video.ID);
        var config = configurationProvider.Load();
        var releaseLocations = video.Files.SelectMany(l => config.GetReleaseFilePaths(applicationPaths, l.ManagedFolder, video, l.RelativePath)).ToHashSet();
        foreach (var releasePath in releaseLocations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(releasePath))
                continue;

            try
            {
                var textData = await File.ReadAllTextAsync(releasePath, cancellationToken);
                var releaseInfo = JsonConvert.DeserializeObject<ReleaseInfoWithProvider>(textData);
                if (releaseInfo is not null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!IsProvider(releaseInfo.ProviderName!))
                        releaseInfo.ProviderName += "+" + Key;
                    return releaseInfo;
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Encountered an error reading release file: {ReleasePath}", releasePath);
            }
        }

        return null;
    }

    private static bool IsProvider(string providerName) =>
        providerName is Key ||
        providerName.StartsWith($"{Key}+") ||
        providerName.EndsWith($"+{Key}") ||
        providerName.Contains($"+{Key}+");
}
