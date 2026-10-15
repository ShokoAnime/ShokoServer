using System.Collections.Generic;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;

namespace Shoko.IntegrationTests;

/// <summary>
/// Titles and overviews to save a creator or character with.
/// </summary>
internal static class TestTexts
{
    /// <summary>
    /// One main title, of no known language.
    /// </summary>
    /// <param name="name">The title.</param>
    /// <returns>The titles.</returns>
    public static IReadOnlyList<ITitle> Named(string name)
        => [Title(name)];

    /// <summary>
    /// A title of a type and language.
    /// </summary>
    /// <param name="name">The title.</param>
    /// <param name="type">Its type.</param>
    /// <param name="languageCode">Its language code.</param>
    /// <returns>The title.</returns>
    public static ITitle Title(string name, TitleType type = TitleType.Main, string languageCode = "unk")
        => new TitleStub
        {
            Source = MetadataSource.Shoko,
            Language = languageCode.TryGetTitleLanguage(out var language) ? language : TitleLanguage.Unknown,
            LanguageCode = languageCode,
            Type = type,
            Value = name,
        };

    /// <summary>
    /// One overview, of no known language.
    /// </summary>
    /// <param name="overview">The overview.</param>
    /// <returns>The overviews.</returns>
    public static IReadOnlyList<IText> Described(string overview)
        => [new TextStub { Source = MetadataSource.Shoko, Language = TitleLanguage.Unknown, LanguageCode = "unk", Value = overview }];
}
