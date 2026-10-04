using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Services;

/// <summary>
///   The one way a title or an overview is chosen out of candidates, for
///   every entry and every source.
/// </summary>
/// <remarks>
///   In order: a user's overall pick; then, language by language, a user's
///   pick for that language and the sources in their order, real titles
///   before generic ones such as <c>Episode 5</c>; then the default; and for
///   an episode with nothing at all, a synthesized title.
/// </remarks>
internal static class TextChooser
{
    #region Titles

    /// <summary>
    ///   Chooses a title out of candidates.
    /// </summary>
    /// <param name="candidates">The candidates, in any order. Disabled and synthesized ones are skipped.</param>
    /// <param name="choice">The orders to walk and the fallbacks.</param>
    /// <returns>The title, or <c>null</c> when nothing qualifies and there is no fallback.</returns>
    internal static ITitle? ChooseTitle(IReadOnlyList<ITitle> candidates, TitleChoice choice)
        => ChooseTitleFor(candidates, choice);

    /// <summary>
    ///   Chooses a title out of candidates, reading every source alike.
    /// </summary>
    /// <param name="candidates">The candidates, in any order. Disabled and synthesized ones are skipped.</param>
    /// <param name="choice">The orders to walk and the fallbacks.</param>
    /// <returns>The title, or <c>null</c> when nothing qualifies and there is no fallback.</returns>
    private static ITitle? ChooseTitleFor(IReadOnlyList<ITitle> candidates, TitleChoice choice)
    {
        var enabled = candidates.Where(title => title.IsEnabled && !title.IsSynthesized).ToList();
        if (enabled.FirstOrDefault(title => title.Preference is TextPreference.Overall) is { } overall)
            return overall;

        // Real titles in every language come before generic ones in any, so a real second-language
        // title beats "Episode 5" in the first.
        foreach (var language in choice.Languages)
        {
            if (language is not TitleLanguage.Main &&
                enabled.FirstOrDefault(title => title.Preference is TextPreference.Language && Speaks(title, language)) is { } picked)
                return picked;

            foreach (var source in choice.SourceOrder)
            {
                if (Walk(enabled, source, language, choice.UseSynonyms, choice.RankGeneric ? false : null, choice.Main) is { } title)
                    return title;
            }
        }

        if (choice.RankGeneric)
        {
            foreach (var language in choice.Languages)
                foreach (var source in choice.SourceOrder)
                    if (Walk(enabled, source, language, choice.UseSynonyms, true, choice.Main) is { } title)
                        return title;
        }

        return choice.Default ?? choice.Synthesize?.Invoke();
    }

    /// <summary>
    ///   One source's title in a language: in <c>x-main</c> its main title,
    ///   else its first ranked title in the language, else any other title in
    ///   it when synonyms are allowed.
    /// </summary>
    /// <remarks>
    ///   In <c>x-main</c>, an entry's default title answers for its source
    ///   when the source has no title of type main.
    /// </remarks>
    /// <param name="titles">The enabled candidates.</param>
    /// <param name="source">The source to read.</param>
    /// <param name="language">The language wanted.</param>
    /// <param name="useSynonyms">Whether a synonym may answer.</param>
    /// <param name="generic">
    ///   <c>false</c> for real titles only, <c>true</c> for generic ones only,
    ///   <c>null</c> for both.
    /// </param>
    /// <param name="main">The entry's default title, or <c>null</c>.</param>
    /// <returns>The title, or <c>null</c> when the source has none for the language.</returns>
    private static ITitle? Walk(List<ITitle> titles, MetadataSource source, TitleLanguage language, bool useSynonyms, bool? generic, ITitle? main)
    {
        var pool = titles.Where(title => title.Source == source && (generic is null || GenericEpisodeTitles.LooksGeneric(title.Value) == generic));

        // x-main asks for whatever the source calls its main title, not for a
        // title in a language named "Main".
        if (language is TitleLanguage.Main)
            return pool.FirstOrDefault(title => title.Type is TitleType.Main) ?? MainOf(main, source, generic);

        return Ranked(pool.Where(title => Speaks(title, language)), useSynonyms);
    }

    /// <summary>
    ///   The title a list answers with, the one rule for every source: its
    ///   first main, official or untyped title, else, when synonyms are
    ///   allowed, its first title of any other type.
    /// </summary>
    /// <remarks>
    ///   Untyped titles count as a user's own and most of AniDB's episode
    ///   titles are untyped.
    /// </remarks>
    /// <param name="titles">The titles, in list order.</param>
    /// <param name="useSynonyms">Whether a short title, synonym, title card or kanji reading may answer.</param>
    /// <returns>The title, or <c>null</c> when none qualifies.</returns>
    internal static ITitle? Ranked(IEnumerable<ITitle> titles, bool useSynonyms)
    {
        ITitle? other = null;
        foreach (var title in titles)
        {
            if (IsRanked(title))
                return title;
            other ??= title;
        }

        return useSynonyms ? other : null;
    }

    /// <summary>
    ///   Whether a title's type lets it be preferred without synonyms: main,
    ///   official or none.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <returns>Whether it is ranked.</returns>
    internal static bool IsRanked(ITitle title)
        => title.Type is TitleType.Main or TitleType.Official or TitleType.None;

    /// <summary>
    ///   Chooses the title of an entry whose texts the store keeps, such as a
    ///   plugin's series, reading the entry's own source in every language.
    /// </summary>
    /// <remarks>
    ///   The own source is read as every source is: after the usual choice
    ///   when the source order ranks it, else in each language after the
    ///   ranked sources and before <c>user</c>. Real episode titles come
    ///   before generic ones.
    /// </remarks>
    /// <param name="candidates">The candidates, in list order. Disabled and synthesized ones are skipped.</param>
    /// <param name="ownSource">The entry's own source.</param>
    /// <param name="choice">The orders to walk. Its default and synthesized title are not used.</param>
    /// <returns>The title, or <c>null</c> when none is picked or in a preferred language.</returns>
    internal static ITitle? ChooseStoredTitle(IReadOnlyList<ITitle> candidates, MetadataSource ownSource, TitleChoice choice)
    {
        var enabled = candidates.Where(title => title.IsEnabled && !title.IsSynthesized).ToList();
        var own = enabled.Where(title => title.Source == ownSource).ToList();
        bool?[] passes = choice.RankGeneric ? [false, true] : [null];
        if (choice.SourceOrder.Contains(ownSource))
        {
            if (ChooseTitleFor(enabled, choice with { Default = null, Synthesize = null }) is { } chosen)
                return chosen;

            foreach (var generic in passes)
                foreach (var language in choice.Languages)
                    if (OwnTitle(own, language, generic, choice) is { } title)
                        return title;

            return null;
        }

        if (enabled.FirstOrDefault(title => title.Preference is TextPreference.Overall) is { } overall)
            return overall;

        foreach (var generic in passes)
            foreach (var language in choice.Languages)
            {
                if (generic is not true && language is not TitleLanguage.Main &&
                    enabled.FirstOrDefault(title => title.Preference is TextPreference.Language && Speaks(title, language)) is { } picked)
                    return picked;

                var ownRead = false;
                foreach (var source in choice.SourceOrder)
                {
                    if (source == MetadataSource.User && !ownRead)
                    {
                        ownRead = true;
                        if (OwnTitle(own, language, generic, choice) is { } ownTitle)
                            return ownTitle;
                    }

                    if (Walk(enabled, source, language, choice.UseSynonyms, generic, choice.Main) is { } title)
                        return title;
                }

                if (!ownRead && OwnTitle(own, language, generic, choice) is { } lastTitle)
                    return lastTitle;
            }

        return null;
    }

    /// <summary>
    ///   An entry's own title in a language: its main title, else its default
    ///   title, for <c>x-main</c>, else its ranked title in the language, as
    ///   <see cref="Ranked"/> reads one.
    /// </summary>
    /// <param name="own">The entry's own enabled titles, in list order.</param>
    /// <param name="language">The language wanted.</param>
    /// <param name="generic">
    ///   <c>false</c> for real titles only, <c>true</c> for generic ones only,
    ///   <c>null</c> for both.
    /// </param>
    /// <param name="choice">The choice, which gives the default title and whether synonyms may answer.</param>
    /// <returns>The title, or <c>null</c> when the entry has none for the language.</returns>
    private static ITitle? OwnTitle(List<ITitle> own, TitleLanguage language, bool? generic, TitleChoice choice)
    {
        var pool = own.Where(title => generic is null || GenericEpisodeTitles.LooksGeneric(title.Value) == generic);
        if (language is TitleLanguage.Main)
            return pool.FirstOrDefault(title => title.Type is TitleType.Main) ?? (choice.Main is { } main ? MainOf(main, main.Source, generic) : null);

        return Ranked(pool.Where(title => title.Language == language), choice.UseSynonyms);
    }

    #endregion

    #region Overviews

    /// <summary>
    ///   Chooses an overview out of candidates.
    /// </summary>
    /// <param name="candidates">The candidates, in any order. Disabled ones are skipped.</param>
    /// <param name="languages">The languages to walk, best first.</param>
    /// <param name="sourceOrder">The sources to walk in each language, best first.</param>
    /// <param name="fallback">The overview to use when none qualifies, or <c>null</c>.</param>
    /// <returns>The overview, or the fallback.</returns>
    internal static IText? ChooseOverview(IReadOnlyList<IText> candidates, IReadOnlyList<TitleLanguage> languages, IReadOnlyList<MetadataSource> sourceOrder, IText? fallback)
    {
        var enabled = candidates.Where(text => text.IsEnabled).ToList();
        if (enabled.FirstOrDefault(text => text.Preference is TextPreference.Overall) is { } overall)
            return overall;

        foreach (var language in languages)
        {
            if (enabled.FirstOrDefault(text => text.Preference is TextPreference.Language && Speaks(text, language)) is { } picked)
                return picked;

            foreach (var source in sourceOrder)
                if (enabled.FirstOrDefault(text => text.Source == source && Speaks(text, language)) is { } hit)
                    return hit;
        }

        return fallback;
    }

    /// <summary>
    ///   Chooses the overview of an entry whose texts the store keeps, reading
    ///   the entry's own source in every language.
    /// </summary>
    /// <remarks>
    ///   When the source order ranks the own source, the choice runs as usual
    ///   and the own source's overview in each language comes after it.
    ///   Otherwise the own source is read in each language after the ranked
    ///   sources and before <c>user</c>.
    /// </remarks>
    /// <param name="candidates">The candidates, in list order. Disabled ones are skipped.</param>
    /// <param name="ownSource">The entry's own source.</param>
    /// <param name="languages">The languages to walk, best first.</param>
    /// <param name="sourceOrder">The sources to walk in each language, best first.</param>
    /// <returns>The overview, or <c>null</c> when none is picked or in a preferred language.</returns>
    internal static IText? ChooseStoredOverview(IReadOnlyList<IText> candidates, MetadataSource ownSource, IReadOnlyList<TitleLanguage> languages, IReadOnlyList<MetadataSource> sourceOrder)
    {
        var enabled = candidates.Where(text => text.IsEnabled).ToList();
        var own = enabled.Where(text => text.Source == ownSource).ToList();
        if (sourceOrder.Contains(ownSource))
            return ChooseOverview(enabled, languages, sourceOrder, null)
                ?? languages.Select(language => own.FirstOrDefault(text => text.Language == language)).FirstOrDefault(text => text is not null);

        if (enabled.FirstOrDefault(text => text.Preference is TextPreference.Overall) is { } overall)
            return overall;

        foreach (var language in languages)
        {
            if (enabled.FirstOrDefault(text => text.Preference is TextPreference.Language && Speaks(text, language)) is { } picked)
                return picked;

            var ownRead = false;
            foreach (var source in sourceOrder)
            {
                if (source == MetadataSource.User && !ownRead)
                {
                    ownRead = true;
                    if (own.FirstOrDefault(text => text.Language == language) is { } ownOverview)
                        return ownOverview;
                }

                if (enabled.FirstOrDefault(text => text.Source == source && Speaks(text, language)) is { } hit)
                    return hit;
            }

            if (!ownRead && own.FirstOrDefault(text => text.Language == language) is { } lastOverview)
                return lastOverview;
        }

        return null;
    }

    #endregion

    #region Helpers

    /// <summary>
    ///   An entry's default title, when it answers <c>x-main</c> for a source.
    /// </summary>
    /// <param name="main">The entry's default title, or <c>null</c>.</param>
    /// <param name="source">The source being read.</param>
    /// <param name="generic">
    ///   <c>false</c> for a real title only, <c>true</c> for a generic one
    ///   only, <c>null</c> for both.
    /// </param>
    /// <returns>The title, or <c>null</c> when it is not the source's or not wanted.</returns>
    private static ITitle? MainOf(ITitle? main, MetadataSource source, bool? generic)
        => main is not null && main.IsEnabled && main.Source == source && (generic is null || GenericEpisodeTitles.LooksGeneric(main.Value) == generic)
            ? main
            : null;

    /// <summary>
    ///   Whether a text is in a language, by its language or by its codes.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="language">The language wanted.</param>
    /// <returns>Whether it is in that language.</returns>
    internal static bool Speaks(IText text, TitleLanguage language)
    {
        if (text.Language == language)
            return true;

        var (languageCode, countryCode) = language.GetLanguageAndCountryCode();
        return string.Equals(text.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrEmpty(countryCode) || string.Equals(text.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}

/// <summary>
///   What <see cref="TextChooser.ChooseTitle"/> walks, and what it falls back
///   to.
/// </summary>
/// <param name="Languages">The languages to walk, best first; <c>x-main</c> reads each source's main title.</param>
/// <param name="SourceOrder">The sources to walk in each language, best first.</param>
/// <param name="UseSynonyms">Whether a title other than a main, official or untyped one may answer for a language.</param>
/// <param name="RankGeneric">Whether generic titles such as <c>Episode 5</c> come after every real one, as for episodes.</param>
/// <param name="Default">The title to use when none qualifies, or <c>null</c>.</param>
/// <param name="Synthesize">Synthesizes a title when there is not even a default, or <c>null</c> to go without.</param>
/// <param name="Main">
///   The entry's default title, which answers <c>x-main</c> for its source
///   when that source has no title of type main, and no other language; or
///   <c>null</c>.
/// </param>
internal sealed record TitleChoice(
    IReadOnlyList<TitleLanguage> Languages,
    IReadOnlyList<MetadataSource> SourceOrder,
    bool UseSynonyms,
    bool RankGeneric,
    ITitle? Default = null,
    Func<ITitle?>? Synthesize = null,
    ITitle? Main = null
);
