
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.Hashing;

namespace Shoko.Plugin.ConfigurationHell.Hashing;

/// <summary>
/// Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor
/// incididunt ut labore et dolore magna aliqua.
/// </summary>
public class ProviderB : IHashProvider
{
    /// <inheritdoc />
    public string Name => "Provider B";

    /// <inheritdoc />
    public string Description => """
        Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor
        incididunt ut labore et dolore magna aliqua.
    """;

    /// <inheritdoc />
    public Version Version => Assembly.GetExecutingAssembly().GetName().Version!;

    /// <inheritdoc />
    public IReadOnlySet<string> AvailableHashTypes => new HashSet<string>(["ED2K", "HASH_B"]);

    /// <inheritdoc />
    public Task<IReadOnlyCollection<HashDigest>> GetHashesForVideo(HashingRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<HashDigest>>([]);
}
