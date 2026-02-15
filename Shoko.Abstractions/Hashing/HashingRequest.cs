
using System.Collections.Generic;
using System.IO;

namespace Shoko.Abstractions.Hashing;

/// <summary>
/// A request for a video file to be hashed.
/// </summary>
public class HashingRequest
{
    /// <summary>
    /// The <see cref="FileInfo"/> of the video file to get hashes for.
    /// </summary>
    public required FileInfo File { get; init; }

    /// <summary>
    /// A list of all existing hashes that the provider supports for the video.
    /// </summary>
    public IReadOnlyList<IHashDigest> ExistingHashes { get; init; } = [];

    /// <summary>
    /// A list of all enabled hash types for this hashing session and provider. 
    /// </summary>
    public required IReadOnlySet<string> EnabledHashTypes { get; init; }

    /// <summary>
    /// Deconstructs the <see cref="HashingRequest"/>.
    /// </summary>
    /// <param name="file">The <see cref="FileInfo"/> of the video file to get hashes for.</param>
    /// <param name="existingHashes">A list of all existing hashes that the provider supports for the video.</param>
    /// <param name="enabledHashTypes">A list of all enabled hash types for this hashing session and provider.</param>
    public void Deconstruct(out FileInfo file, out IReadOnlyList<IHashDigest> existingHashes, out IReadOnlySet<string> enabledHashTypes)
    {
        file = File;
        existingHashes = ExistingHashes;
        enabledHashTypes = EnabledHashTypes;
    }
}
