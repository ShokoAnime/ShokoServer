using System.Collections.Generic;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   A subtitle track served as a resource of a stream session.
/// </summary>
public class StreamSubtitleDescription
{
    /// <summary>
    ///   The position of the track among the subtitle tracks, counting from
    ///   zero.
    /// </summary>
    public required int Ordinal { get; init; }

    /// <summary>
    ///   The format of the resource, e.g. <c>ass</c>, <c>ssa</c>, <c>srt</c>,
    ///   <c>vtt</c>, <c>pgs</c> or <c>vobsub</c>.
    /// </summary>
    public required string Format { get; init; }

    /// <summary>
    ///   The path of the resource, relative to the session.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    ///   The paths of any further files the track needs, relative to the
    ///   session, e.g. the <c>.sub</c> of a VobSub pair.
    /// </summary>
    public IReadOnlyList<string> CompanionPaths { get; init; } = [];

    /// <summary>
    ///   Optional. The language of the track, as a BCP 47 tag.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    ///   Optional. The title of the track.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    ///   Whether the track is shown when the client picks none.
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    ///   Whether the track should always be shown, e.g. signs and songs.
    /// </summary>
    public bool IsForced { get; init; }

    /// <summary>
    ///   Whether the track comes from a file beside the video rather than from
    ///   its container.
    /// </summary>
    public bool IsExternal { get; init; }

    /// <summary>
    ///   Optional. The preference order of the track among the subtitle tracks
    ///   for the requesting user, best first, counting from zero.
    /// </summary>
    public int? Rank { get; init; }
}
