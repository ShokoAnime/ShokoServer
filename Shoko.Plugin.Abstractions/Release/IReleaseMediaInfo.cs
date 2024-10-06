using System.Collections.Generic;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions.Release;

/// <summary>
/// Represents the audio and subtitle languages associated with an <see cref="IReleaseInfo"/>.
/// /// </summary>
public interface IReleaseMediaInfo
{
    /// <summary>
    /// Gets the audio languages associated with the media file.
    /// </summary>
    IReadOnlyList<TitleLanguage> AudioLanguages { get; }

    /// <summary>
    /// Gets the subtitle languages associated with the media file.
    /// </summary>
    IReadOnlyList<TitleLanguage> SubtitleLanguages { get; }
}
