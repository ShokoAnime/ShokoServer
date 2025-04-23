using System;
using System.IO;
using System.Reflection;
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
public class ReleaseImporter(ILogger<ReleaseImporter> logger, IApplicationPaths applicationPaths, ConfigurationProvider<ReleaseExporterConfiguration> configurationProvider) : IReleaseInfoProvider<ReleaseExporterConfiguration>
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
    public Version Version { get; private set; } = Assembly.GetExecutingAssembly().GetName().Version ?? new("0.0.0");

    /// <inheritdoc/>
    public Task<ReleaseInfo?> GetReleaseInfoById(string releaseId, CancellationToken cancellationToken)
        => Task.FromResult<ReleaseInfo?>(null);

    /// <inheritdoc/>
    public async Task<ReleaseInfo?> GetReleaseInfoForVideo(IVideo video, CancellationToken cancellationToken)
    {
        logger.LogTrace("Trying to find release for video. (Video={VideoID})", video.ID);
        var config = configurationProvider.Load();
        foreach (var location in video.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var releasePath = config.GetReleaseFilePath(applicationPaths, location.ManagedFolder, video, location.RelativePath);
            if (!File.Exists(releasePath))
                continue;

            try
            {
                var textData = await File.ReadAllTextAsync(releasePath, cancellationToken);
                var releaseInfo = JsonConvert.DeserializeObject<ReleaseInfoWithProvider>(textData);
                if (releaseInfo is not null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
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
}
