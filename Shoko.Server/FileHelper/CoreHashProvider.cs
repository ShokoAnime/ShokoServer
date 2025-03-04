
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.Hashing;

namespace Shoko.Server.FileHelper;

/// <summary>
///    Responsible for providing the core hashes for a video. Among them, only
///    ED2K is required to be calculated, but not necessarily by this provider.
/// </summary>
public class CoreHashProvider : IHashProvider
{
    /// <inheritdoc/>
    public string Name => "Core";

    /// <inheritdoc/>
    public Version Version => Assembly.GetExecutingAssembly().GetName().Version!;

    /// <inheritdoc/>
    public IReadOnlySet<string> AvailableHashTypes => new HashSet<string>() { "ED2K", "MD5", "CRC32", "SHA1", "SHA256", "SHA512" };

    /// <inheritdoc/>
    public IReadOnlySet<string> DefaultEnabledHashTypes => new HashSet<string>() { "ED2K" };

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<HashDigest>> GetHashesForVideo(HashingRequest request, CancellationToken cancellationToken = default)
    {
        var (file, existingHashes, enabledHashTypes) = request;
        var newHashes = await Task.Run(() => Hasher.CalculateHashes(
            file.FullName,
            !existingHashes.Any(h => h.Type is "ED2K") && enabledHashTypes.Contains("ED2K"),
            !existingHashes.Any(h => h.Type is "CRC32") && enabledHashTypes.Contains("CRC32"),
            !existingHashes.Any(h => h.Type is "MD5") && enabledHashTypes.Contains("MD5"),
            !existingHashes.Any(h => h.Type is "SHA1") && enabledHashTypes.Contains("SHA1"),
            !existingHashes.Any(h => h.Type is "SHA256") && enabledHashTypes.Contains("SHA256"),
            !existingHashes.Any(h => h.Type is "SHA512") && enabledHashTypes.Contains("SHA512"),
            cancellationToken
        ), cancellationToken);
        return newHashes
            .Concat(existingHashes.Select(h => new HashDigest() { Type = h.Type, Value = h.Value, Metadata = h.Metadata }))
            .DistinctBy(h => h.Type)
            .OrderBy(h => (h.Type, h.Value, h.Metadata))
            .ToList();
    }
}
