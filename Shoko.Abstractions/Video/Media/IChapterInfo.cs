using System;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Video.Media;

/// <summary>
///   A chapter of a media container, with its names in every language the
///   file gives. <see cref="IWithTitles.DefaultTitle"/> is the first name the
///   file lists, and <see cref="IWithTitles.PreferredTitle"/> follows the
///   episode title language order.
/// </summary>
public interface IChapterInfo : IWithTitles
{
    /// <summary>
    ///   Where the chapter starts, from the start of the file.
    /// </summary>
    TimeSpan Timestamp { get; }
}
