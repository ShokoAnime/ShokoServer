using System;
using Shoko.Abstractions.Enums;

namespace Shoko.Abstractions.Video.Media;

/// <summary>
/// Chapter information.
/// </summary>
public interface IChapterInfo
{
    /// <summary>
    /// Chapter title.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// <see cref="TitleLanguage"/> name of the language the chapter information.
    /// </summary>
    TitleLanguage Language { get; }

    /// <summary>
    /// 3 character language code of the language the chapter information.
    /// </summary>
    string? LanguageCode { get; }

    /// <summary>
    /// Chapter timestamp.
    /// </summary>
    TimeSpan Timestamp { get; }
}
