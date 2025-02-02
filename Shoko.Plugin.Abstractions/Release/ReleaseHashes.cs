using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Release;

/// <summary>
/// Hash container object.
/// </summary>
public class ReleaseHashes : IHashes
{
    /// <inheritdoc/>
    public string? CRC { get; set; }

    /// <inheritdoc/>
    public string? MD5 { get; set; }

    /// <inheritdoc/>
    public string ED2K { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string? SHA1 { get; set; }

    /// <inheritdoc/>
    public string? this[HashAlgorithmName algorithm]
        => algorithm switch
        {
            HashAlgorithmName.ED2K => ED2K,
            HashAlgorithmName.MD5 => MD5,
            HashAlgorithmName.SHA1 => SHA1,
            HashAlgorithmName.CRC32 => CRC,
            _ => null,
        };

    /// <summary>
    /// Creates a new instance of <see cref="ReleaseHashes"/>.
    /// </summary>
    public ReleaseHashes() { }

    /// <summary>
    /// Creates a new instance of <see cref="ReleaseHashes"/> based on the given <paramref name="hashes"/>.
    /// </summary>
    public ReleaseHashes(IHashes hashes)
    {
        CRC = hashes.CRC;
        MD5 = hashes.MD5;
        ED2K = hashes.ED2K;
        SHA1 = hashes.SHA1;
    }
}
