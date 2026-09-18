using System;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   A chapter of a stream session.
/// </summary>
public class StreamChapterDescription
{
    /// <summary>
    ///   Where the chapter starts.
    /// </summary>
    public required TimeSpan Start { get; init; }

    /// <summary>
    ///   Optional. Where the chapter ends.
    /// </summary>
    public TimeSpan? End { get; init; }

    /// <summary>
    ///   Optional. The title of the chapter.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    ///   Optional. The language of the title, as a BCP 47 tag.
    /// </summary>
    public string? Language { get; init; }
}
