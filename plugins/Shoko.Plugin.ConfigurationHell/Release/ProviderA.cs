using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Release;

namespace Shoko.Plugin.ConfigurationHell.Release;

/// <summary>
/// Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor
/// incididunt ut labore et dolore magna aliqua.
/// </summary>
public class ProviderA : IReleaseInfoProvider
{
    /// <inheritdoc />
    public string Name => "Provider A";

    /// <inheritdoc />
    public string Description => """
        Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor
        incididunt ut labore et dolore magna aliqua.
    """;

    /// <inheritdoc />
    public Version Version => new(1, 2, 0);

    /// <inheritdoc />
    public Task<ReleaseInfo?> GetReleaseInfoById(string releaseId, CancellationToken cancellationToken)
        => Task.FromResult<ReleaseInfo?>(null);

    /// <inheritdoc />
    public Task<ReleaseInfo?> GetReleaseInfoForVideo(IVideo video, CancellationToken cancellationToken)
        => Task.FromResult<ReleaseInfo?>(null);
}
