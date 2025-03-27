using System.Collections.Generic;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions.Release;

/// <summary>
/// Represents the audio and subtitle languages associated with an <see cref="IReleaseInfo"/>.
/// /// </summary>
public class ReleaseMediaInfo : IReleaseMediaInfo
{
    /// <summary>
    /// Gets the audio languages associated with the media file.
    /// </summary>
    public List<TitleLanguage> AudioLanguages { get; set; } = [];

    /// <summary>
    /// Gets the subtitle languages associated with the media file.
    /// </summary>
    public List<TitleLanguage> SubtitleLanguages { get; set; } = [];

    /// <inheritdoc />
    public ReleaseMediaInfo() { }

    /// <summary>
    /// Constructs a new <see cref="ReleaseMediaInfo"/> instance from a
    /// <see cref="IReleaseMediaInfo"/>.
    /// </summary>
    /// <param name="info">The <see cref="IReleaseMediaInfo"/> to construct
    /// from.</param>
    public ReleaseMediaInfo(IReleaseMediaInfo info)
    {
        AudioLanguages = [.. info.AudioLanguages];
        SubtitleLanguages = [.. info.SubtitleLanguages];
    }

    #region IReleaseMediaInfo implementation

    /// <inheritdoc />
    IReadOnlyList<TitleLanguage> IReleaseMediaInfo.AudioLanguages => AudioLanguages;

    /// <inheritdoc />
    IReadOnlyList<TitleLanguage> IReleaseMediaInfo.SubtitleLanguages => SubtitleLanguages;

    #endregion
}
