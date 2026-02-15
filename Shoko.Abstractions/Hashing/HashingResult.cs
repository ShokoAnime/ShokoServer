
using System.Collections.Generic;
using Shoko.Abstractions.Video;

namespace Shoko.Abstractions.Hashing;

/// <summary>
/// The result of hashing a video file.
/// </summary>
public class HashingResult
{
    /// <summary>
    /// Indicates that the hashes may have been reused from an existing video.
    /// </summary>
    public required bool UsedExistingHashes { get; init; }

    /// <summary>
    /// Indicates that the video was just added to the database as a result of
    /// this operation.
    /// </summary>
    public required bool IsNewVideo { get; init; }

    /// <summary>
    /// Indicates that the file was just added to the database as a result of
    /// this operation.
    /// </summary>
    public required bool IsNewFile { get; init; }

    /// <summary>
    /// The video that was hashed.
    /// </summary>
    public required IVideo Video { get; init; }

    /// <summary>
    /// The video file that was hashed.
    /// </summary>
    public required IVideoFile File { get; init; }

    /// <summary>
    /// The hashes that were the result of the operation. May or may not have
    /// been reused depending on the provider(s) enabled and if it was requested
    /// to re-use existing hashes.
    /// </summary>
    public required IReadOnlyList<IHashDigest> Hashes { get; init; }

    /// <summary>
    /// Deconstructs the <see cref="HashingResult"/>.
    /// </summary>
    /// <param name="video">The video that was hashed.</param>
    /// <param name="file">The video file that was hashed.</param>
    /// <param name="hashes">The hashes that were the result of the operation.</param>
    public void Deconstruct(out IVideo video, out IVideoFile file, out IReadOnlyList<IHashDigest> hashes)
    {
        video = Video;
        file = File;
        hashes = Hashes;
    }
}
