using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Hashing;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Hash container object.
/// </summary>
public interface IHashes
{
    /// <summary>
    /// Gets the ED2K hash.
    /// </summary>
    string ED2K { get; }

    /// <summary>
    /// Gets the CRC32 hash if it's available.
    /// </summary>
    string? CRC32 { get; }

    /// <summary>
    /// Gets the MD5 hash if it's available.
    /// </summary>
    string? MD5 { get; }

    /// <summary>
    /// Gets the SHA1 hash if it's available.
    /// </summary>
    string? SHA1 { get; }

    /// <summary>
    /// Gets the SHA256 hash if it's available.
    /// </summary>
    string? SHA256 { get; }

    /// <summary>
    /// Gets the SHA512 hash if it's available.
    /// </summary>
    string? SHA512 { get; }

    /// <summary>
    /// All stored hashes for the video.
    /// </summary>
    IReadOnlyList<IHashDigest> Hashes { get; }
}
