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
public class ProviderC : IHashProvider
{
    /// <inheritdoc />
    public string Name => "Provider C";

    /// <inheritdoc />
    public Version Version => Assembly.GetExecutingAssembly().GetName().Version!;

    /// <inheritdoc />
    public IReadOnlySet<string> AvailableHashTypes => new HashSet<string>(["HASH_C", "HaSh_D", "HAsh_E", "Has_H_F"]);

    /// <inheritdoc />
    public IReadOnlySet<string> DefaultEnabledHashTypes => new HashSet<string>([]);

    /// <inheritdoc />
    public Task<IReadOnlyCollection<HashDigest>> GetHashesForVideo(HashingRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<HashDigest>>([]);
}
