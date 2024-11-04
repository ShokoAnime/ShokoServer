using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Hash container object.
/// </summary>
public interface IHashes
{
    /// <summary>
    /// Gets the CRC32 hash if it's available.
    /// </summary>
    string? CRC { get; }

    /// <summary>
    /// Gets the MD5 hash if it's available.
    /// </summary>
    string? MD5 { get; }

    /// <summary>
    /// Gets the ED2K hash.
    /// </summary>
    string ED2K { get; }

    /// <summary>
    /// Gets the SHA1 hash if it's available.
    /// </summary>
    string? SHA1 { get; }

    /// <summary>
    /// Gets the hash for the specified algorithm.
    /// </summary>
    string? this[HashAlgorithmName algorithm] { get; }
}
