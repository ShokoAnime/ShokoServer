using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Release;

namespace Shoko.Plugin.ReleaseExporter;

/// <summary>
/// Responsible for importing releases from the file system near the video files.
/// </summary>
/// <param name="logger">Logger.</param>
public class ReleaseImporter(ILogger<ReleaseImporter> logger) : IReleaseInfoProvider
{
    /// <inheritdoc/>
    public const string Key = "Release Importer";

    /// <inheritdoc/>
    public string Name => Key;

    /// <inheritdoc/>
    public Version Version { get; private set; } = Assembly.GetExecutingAssembly().GetName().Version ?? new("0.0.0");

    /// <inheritdoc/>
    public Task<ReleaseInfo?> GetReleaseInfoById(string releaseId, CancellationToken cancellationToken)
        => Task.FromResult<ReleaseInfo?>(null);

    /// <inheritdoc/>
    public async Task<ReleaseInfo?> GetReleaseInfoForVideo(IVideo video, CancellationToken cancellationToken)
    {
        logger.LogTrace("Trying to find release for video. (Video={VideoID})", video.ID);
        foreach (var location in video.Locations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = location.Path;
            var releasePath = Path.ChangeExtension(path, ".release.json");
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
