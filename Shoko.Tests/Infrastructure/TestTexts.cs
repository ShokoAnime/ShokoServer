using System.Collections.Generic;
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
    /// One English overview.
    /// </summary>
    /// <param name="overview">The overview.</param>
    /// <returns>The overviews.</returns>
    public static IReadOnlyList<IText> Described(string overview)
        => [new TextStub { Source = TestSources.Plugin, Language = TitleLanguage.English, LanguageCode = "en", Value = overview }];
}
