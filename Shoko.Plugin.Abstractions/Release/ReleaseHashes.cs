using Shoko.Plugin.Abstractions.DataModels;

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
