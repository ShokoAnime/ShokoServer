using System.Collections.Generic;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// The titles and overviews tests save with an entry, in English.
/// </summary>
public static class TestTexts
{
    /// <summary>
    /// One English main title.
    /// </summary>
    /// <param name="name">The title.</param>
    /// <returns>The titles.</returns>
    public static IReadOnlyList<ITitle> Named(string name)
        => [new TitleStub { Source = TestSources.Plugin, Language = TitleLanguage.English, LanguageCode = "en", Value = name, Type = TitleType.Main }];

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
            Source = TestSources.Plugin,
            Language = languageCode.TryGetTitleLanguage(out var language) ? language : TitleLanguage.Unknown,
            LanguageCode = languageCode,
            Value = name,
            Type = type,
        };

    /// <summary>
    /// One English overview.
    /// </summary>
    /// <param name="overview">The overview.</param>
    /// <returns>The overviews.</returns>
    public static IReadOnlyList<IText> Described(string overview)
        => [new TextStub { Source = TestSources.Plugin, Language = TitleLanguage.English, LanguageCode = "en", Value = overview }];
}
