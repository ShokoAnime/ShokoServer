using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Release;

namespace Shoko.Plugin.ConfigurationHell.Release;

/// <summary>
/// Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor
/// incididunt ut labore et dolore magna aliqua.
/// </summary>
public class ProviderB : IReleaseInfoProvider<ProviderB.ProviderBConfig>
{
    /// <inheritdoc />
    public string Name => "Provider B";

    /// <inheritdoc />
    public Version Version => new(2, 3, 0);

    /// <inheritdoc />
    public Task<ReleaseInfo?> GetReleaseInfoById(string releaseId, CancellationToken cancellationToken)
        => Task.FromResult<ReleaseInfo?>(null);

    /// <inheritdoc />
    public Task<ReleaseInfo?> GetReleaseInfoForVideo(IVideo video, CancellationToken cancellationToken)
        => Task.FromResult<ReleaseInfo?>(null);

    /// <summary>
    /// Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod
    /// tempor incididunt ut labore et dolore magna aliqua.
    /// </summary>
    public class ProviderBConfig : IReleaseInfoProviderConfiguration
    {
        /// <summary>
        /// Lorem ipsum dolor sit amet consectetur adipiscing elit sed do
        /// eiusmod tempor incididunt ut labore et dolore magna aliqua.
        /// </summary>
        public bool RandomSettingA { get; set; }

        /// <summary>
        /// Lorem ipsum dolor sit amet consectetur adipiscing elit sed do
        /// eiusmod tempor incididunt ut labore et dolore magna aliqua.
        /// </summary>
        public bool RandomSettingB { get; set; }
    }
}
