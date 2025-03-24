using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Config.Attributes;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Hashing;

namespace Shoko.Plugin.ConfigurationHell.Hashing;

/// <summary>
/// Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor
/// incididunt ut labore et dolore magna aliqua.
/// </summary>
public class ProviderA : IHashProvider<ProviderA.ProviderAConfig>
{
    /// <inheritdoc />
    public string Name => "Provider A";

    /// <inheritdoc />
    public string Description => """
        Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor
        incididunt ut labore et dolore magna aliqua.
    """;

    /// <inheritdoc />
    public Version Version => Assembly.GetExecutingAssembly().GetName().Version!;

    /// <inheritdoc />
    public IReadOnlySet<string> AvailableHashTypes => new HashSet<string>(["HASH_A"]);

    /// <inheritdoc />
    public Task<IReadOnlyCollection<HashDigest>> GetHashesForVideo(HashingRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<HashDigest>>([]);
    }

    /// <summary>
    /// Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod
    /// tempor incididunt ut labore et dolore magna aliqua.
    /// </summary>
    public class ProviderAConfig : IHashProviderConfiguration
    {
        /// <summary>
        /// Example JSON config.
        /// </summary>
        [CodeEditor(CodeLanguage.Json, AutoFormatOnLoad = true)]
        public string? JsonSetting { get; set; }
    }
}
