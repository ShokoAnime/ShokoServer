using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Plugin.Tmdb.Mapping;

/// <summary>
///   The languages a refresh keeps TMDb's translations and content ratings
///   in. A <c>null</c> set keeps them all.
/// </summary>
/// <param name="Titles">The languages of the titles of anything but an episode.</param>
/// <param name="EpisodeTitles">The languages of an episode's titles.</param>
/// <param name="Overviews">The languages of the overviews and biographies.</param>
/// <param name="ContentRatings">The languages of the countries whose content ratings are kept.</param>
public sealed record TmdbTextLanguages(
    IReadOnlySet<TitleLanguage>? Titles,
    IReadOnlySet<TitleLanguage>? EpisodeTitles,
    IReadOnlySet<TitleLanguage>? Overviews,
    IReadOnlySet<TitleLanguage>? ContentRatings
)
{
    /// <summary>
    ///   Keeps everything TMDb lists.
    /// </summary>
    public static TmdbTextLanguages All { get; } = new(null, null, null, null);

    /// <summary>
    ///   The languages the configuration and the server's language order
    ///   keep, as the core's TMDB settings did.
    /// </summary>
    /// <param name="configuration">The plugin's configuration.</param>
    /// <param name="textManager">The core's text manager, which knows the language order.</param>
    /// <returns>The languages.</returns>
    public static TmdbTextLanguages From(TmdbConfiguration configuration, IMetadataTextManager textManager)
    {
        var titles = textManager.GetLanguageOrder(TextKind.Title, MetadataEntityType.Series);
        var episodes = textManager.GetLanguageOrder(TextKind.Title, MetadataEntityType.Episode);
        return new(
            TmdbTexts.KeptLanguages(configuration.DownloadAllTitles, titles),
            TmdbTexts.KeptLanguages(configuration.DownloadAllTitles, episodes),
            TmdbTexts.KeptLanguages(configuration.DownloadAllOverviews, textManager.GetLanguageOrder(TextKind.Overview)),
            configuration.DownloadAllContentRatings
                ? null
                : TmdbTexts.KeptLanguages(false, titles.Concat(episodes).Append(TitleLanguage.EnglishAmerican))
        );
    }
}
