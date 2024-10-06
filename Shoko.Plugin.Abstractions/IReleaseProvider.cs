using System.Threading;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Release;

namespace Shoko.Plugin.Abstractions;

/// <summary>
/// Video release information provider.
/// </summary>
public interface IReleaseInfoProvider
{
    /// <summary>
    /// Gets the friendly name of the release information provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the release information for the specified video, if available from
    /// the provider.
    /// </summary>
    /// <param name="video">The video.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    Task<ReleaseInfo?> GetReleaseInfoForVideo(IVideo video, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the release information by a specified release id, if the provider
    /// supports it.
    /// </summary>
    /// <param name="releaseId">The release id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<ReleaseInfo?> GetReleaseInfoById(string releaseId, CancellationToken cancellationToken);
}
