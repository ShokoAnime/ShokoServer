
using System;
using System.Collections.Generic;
using System.IO;
using Shoko.Abstractions.Hashing;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Release;
using Shoko.Abstractions.User;
using Shoko.Abstractions.UserData;
using Shoko.Abstractions.Video.Media;

namespace Shoko.Abstractions.Video;

/// <summary>
/// Video.
/// </summary>
public interface IVideo : IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    /// Video ID.
    /// </summary>
    int ID { get; }

    /// <summary>
    /// The earliest known local file name of the video.
    /// </summary>
    string? EarliestKnownName { get; }

    /// <summary>
    /// The ED2K hash for the file.
    /// </summary>
    string ED2K { get; }

    /// <summary>
    /// The file size, in bytes.
    /// </summary>
    long Size { get; }

    /// <summary>
    /// Indicates this file is marked as a variation in Shoko.
    /// </summary>
    public bool IsVariation { get; set; }

    /// <summary>
    /// When the video was last imported by shoko. Usually a video is only
    /// imported once, but there may be exceptions, e.g. if it's unlinked and
    /// then linked again.
    /// </summary>
    DateTime? ImportedAt { get; }

    /// <summary>
    /// All video files for the video.
    /// </summary>
    IReadOnlyList<IVideoFile> Files { get; }

    /// <summary>
    /// The current release information for the video, if the video has an
    /// existing release associated with it. All recognized videos have a
    /// release associated with them.
    /// </summary>
    IReleaseInfo? ReleaseInfo { get; }

    /// <summary>
    /// All stored hashes for the video, including the ED2K which is mandatory
    /// and has a dedicated field/member above.
    /// </summary>
    IReadOnlyList<IHashDigest> Hashes { get; }

    /// <summary>
    /// The MediaInfo data for the file. This can be null, but it shouldn't be.
    /// </summary>
    IMediaInfo? MediaInfo { get; }

    /// <summary>
    /// All cross-references linked to the video.
    /// </summary>
    IReadOnlyList<IVideoCrossReference> CrossReferences { get; }

    /// <summary>
    /// All episodes linked to the video.
    /// </summary>
    IReadOnlyList<IShokoEpisode> Episodes { get; }

    /// <summary>
    /// All shows linked to the show.
    /// </summary>
    IReadOnlyList<IShokoSeries> Series { get; }

    /// <summary>
    /// Information about the group
    /// </summary>
    IReadOnlyList<IShokoGroup> Groups { get; }

    /// <summary>
    /// Get the stream for the video, if any files are still available.
    /// </summary>
    Stream? GetStream();

    /// <summary>
    ///   Gets the user-specific data for the video and user if the user have
    ///   any stored data for the video.
    /// </summary>
    /// <param name="user">
    ///   The user to get the data for.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="user"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   Thrown when the <paramref name="user"/> is not stored in the database.
    /// </exception>
    /// <returns>
    ///   The user-specific data for the video and user.
    /// </returns>
    IVideoUserData? GetUserData(IUser user);
}
