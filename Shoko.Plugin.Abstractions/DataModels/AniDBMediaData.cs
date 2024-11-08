using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Represents the audio and subtitle languages associated with an AniDB file.
/// </summary>
public class AniDBMediaData
{
    /// <summary>
    /// Gets the audio languages associated with the media file.
    /// </summary>
    public IReadOnlyList<TitleLanguage> AudioLanguages { get; set; } = new List<TitleLanguage>();

    /// <summary>
    /// Gets the subtitle languages associated with the media file.
    /// </summary>
    public IReadOnlyList<TitleLanguage> SubLanguages { get; set; } = new List<TitleLanguage>();
}
