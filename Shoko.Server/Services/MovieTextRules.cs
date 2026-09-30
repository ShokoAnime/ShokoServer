using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Services;

/// <summary>
///   The fixed rules for when a Shoko series or episode takes its text from
///   the films an anime is linked to on one source. They hold for every
///   source alike and have no settings.
/// </summary>
internal static partial class MovieTextRules
{
    #region Placeholders

    /// <summary>
    ///   What kind of stand-in name an AniDB episode title is.
    /// </summary>
    internal enum PlaceholderKind
    {
        /// <summary>
        ///   The whole work, such as <c>Complete Movie</c>, <c>OVA</c> or
        ///   <c>TV Special</c>.
        /// </summary>
        Whole,

        /// <summary>
        ///   A part of the work, such as <c>Part 1 of 2</c> or <c>Part II</c>.
        /// </summary>
        Part,

        /// <summary>
        ///   A numbered piece, such as <c>Episode 1</c>, <c>Volume 2</c>,
        ///   <c>Movie 1</c> or <c>OVA 2</c>.
        /// </summary>
        Numbered,
    }

    /// <summary>
    ///   An AniDB episode title read as a stand-in name.
    /// </summary>
    /// <param name="Kind">What kind of stand-in it is.</param>
    /// <param name="Label">
    ///   What to keep after a film's title, e.g. <c>Part 1 of 2</c>, or
    ///   <c>null</c> for the whole work.
    /// </param>
    /// <param name="Suffix">
    ///   A trailing label kept after that, e.g. <c>Decide Version</c>, or
    ///   <c>null</c> when there is none.
    /// </param>
    internal sealed record Placeholder(PlaceholderKind Kind, string? Label, string? Suffix);

    private const string Roman = "(?-i:[IVX]+)";

    [GeneratedRegex(@"^(?:Complete\s+(?:Movie|Film|OVA|OAD|ONA|Web|TV\s+Special|Special|Series)|Movie|OVA|OAD|ONA|TV\s+Special)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WholeRegex();

    [GeneratedRegex(@"^Part\s+(?:\d+|" + Roman + @")(?:\s+of\s+(?:\d+|" + Roman + "))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PartRegex();

    [GeneratedRegex(@"^(?:Episode|Volume|Vol\.|Movie|OVA|OAD|ONA|Special|TV\s+Special)\s+\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NumberedRegex();

    [GeneratedRegex(@"^(?<core>.*?\S)\s*\((?<suffix>Part\s+(?:\d+|" + Roman + @")(?:\s+of\s+(?:\d+|" + Roman + @"))?|[^()]*\S\s+Version)\)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SuffixRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceRegex();

    /// <summary>
    ///   Reads an AniDB episode title as a stand-in name, if it is one.
    /// </summary>
    /// <remarks>
    ///   A stand-in may end in a part or version label, e.g.
    ///   <c>Complete Movie (Decide Version)</c>. A real name keeps being one
    ///   with such a label, e.g. <c>Summer Break (Part 1)</c>.
    /// </remarks>
    /// <param name="title">AniDB's English title for the episode.</param>
    /// <returns>The stand-in, or <c>null</c> when the title is a real name or missing.</returns>
    internal static Placeholder? ParsePlaceholder(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var trimmed = SpaceRegex().Replace(title.Trim(), " ");
        if (ParseCore(trimmed) is { } bare)
            return bare;

        if (SuffixRegex().Match(trimmed) is not { Success: true } match || ParseCore(match.Groups["core"].Value) is not { } core)
            return null;

        var suffix = match.Groups["suffix"].Value;
        var isPart = suffix.StartsWith("Part", StringComparison.OrdinalIgnoreCase);
        return core.Kind is PlaceholderKind.Whole && isPart
            ? new(PlaceholderKind.Part, suffix, null)
            : core with { Suffix = suffix };
    }

    /// <summary>
    ///   Reads a title with no trailing label as a stand-in name.
    /// </summary>
    /// <param name="title">The trimmed title.</param>
    /// <returns>The stand-in, or <c>null</c> when the title is not one.</returns>
    private static Placeholder? ParseCore(string title)
    {
        if (WholeRegex().IsMatch(title))
            return new(PlaceholderKind.Whole, null, null);
        if (PartRegex().IsMatch(title))
            return new(PlaceholderKind.Part, title, null);
        if (NumberedRegex().IsMatch(title))
            return new(PlaceholderKind.Numbered, title, null);

        return null;
    }

    /// <summary>
    ///   A film's title with a stand-in's labels kept after it, e.g.
    ///   <c>Vampire Hunter D (Part 1 of 2)</c>.
    /// </summary>
    /// <param name="filmTitle">The film's title.</param>
    /// <param name="placeholder">The stand-in the episode was named.</param>
    /// <returns>The episode's title.</returns>
    internal static string LabelFilmTitle(string filmTitle, Placeholder placeholder)
    {
        var title = placeholder.Label is null ? filmTitle : $"{filmTitle} ({placeholder.Label})";
        return placeholder.Suffix is null ? title : $"{title} ({placeholder.Suffix})";
    }

    #endregion

    #region Series

    /// <summary>
    ///   Whether an anime of a type looks at its film links before its series
    ///   links: movies, web releases and TV specials do; every other type
    ///   looks at its series links first.
    /// </summary>
    /// <param name="type">The anime's type.</param>
    /// <returns>Whether the film links come first.</returns>
    internal static bool MoviesFirst(AnimeType type)
        => type is AnimeType.Movie or AnimeType.Web or AnimeType.TVSpecial;

    /// <summary>
    ///   One AniDB episode and the stored films it is linked to on a source.
    /// </summary>
    /// <param name="AnidbEpisodeID">The AniDB episode.</param>
    /// <param name="Type">The episode's type.</param>
    /// <param name="Title">AniDB's English title for it, or <c>null</c> when it has none.</param>
    /// <param name="Movies">The stored films it is linked to on the source.</param>
    internal sealed record EpisodeFilms(int AnidbEpisodeID, EpisodeType Type, string? Title, IReadOnlyList<MetadataGuid> Movies);

    /// <summary>
    ///   Which film or collection speaks for a whole anime on a source.
    /// </summary>
    /// <remarks>
    ///   Every normal episode must be linked, all to one film or all to films
    ///   of one collection. An anime whose normal episodes are all stand-ins
    ///   for the whole work or its parts, one of them for the whole, is an
    ///   older film entry instead: its linked whole episode names the film,
    ///   and failing that its linked parts are read like normal episodes.
    /// </remarks>
    /// <param name="episodes">Every episode of the anime, with its films on the source.</param>
    /// <param name="collectionsOf">The collections a film is in on the source.</param>
    /// <returns>The film or collection, or <c>null</c> when the source's films say nothing for the anime.</returns>
    internal static MetadataGuid? ChooseSeriesEntry(IReadOnlyList<EpisodeFilms> episodes, Func<MetadataGuid, IReadOnlyList<MetadataGuid>> collectionsOf)
    {
        var read = episodes.Select(episode => (Episode: episode, Placeholder: ParsePlaceholder(episode.Title))).ToList();
        var normal = read.Where(entry => entry.Episode.Type is EpisodeType.Episode).ToList();
        if (normal.Count is 0)
            return null;

        // Parts with no whole episode beside them are read like any other
        // normal episodes, so each of them must be linked.
        var wholes = normal.Where(entry => entry.Placeholder?.Kind is PlaceholderKind.Whole).ToList();
        var filmEntry = wholes.Count is not 0 && normal.All(entry => entry.Placeholder?.Kind is PlaceholderKind.Whole or PlaceholderKind.Part);
        if (!filmEntry)
            return normal.All(entry => entry.Episode.Movies.Count > 0) ? OneFilmOrCollection(normal.Select(entry => entry.Episode), collectionsOf) : null;

        // The normal whole episode names the film over its parts' links; two naming two films say
        // nothing. A whole stand-in of another type (a TV special) is its own work, never the anime.
        var wholeFilms = wholes
            .SelectMany(entry => entry.Episode.Movies)
            .Distinct()
            .Take(2)
            .ToList();
        if (wholeFilms.Count is not 0)
            return wholeFilms is [var only] ? only : null;

        // Parts of any type belong to the whole work; the unlinked ones are
        // left out.
        var linkedParts = read
            .Where(entry => entry.Placeholder?.Kind is PlaceholderKind.Part && entry.Episode.Movies.Count > 0)
            .Select(entry => entry.Episode)
            .ToList();
        return linkedParts.Count is 0 ? null : OneFilmOrCollection(linkedParts, collectionsOf);
    }

    /// <summary>
    ///   The one film the episodes are linked to, or the one collection
    ///   holding every film they are linked to.
    /// </summary>
    /// <param name="episodes">The episodes, each linked to at least one film.</param>
    /// <param name="collectionsOf">The collections a film is in on the source.</param>
    /// <returns>The film or collection, or <c>null</c> when there is no single one.</returns>
    private static MetadataGuid? OneFilmOrCollection(IEnumerable<EpisodeFilms> episodes, Func<MetadataGuid, IReadOnlyList<MetadataGuid>> collectionsOf)
    {
        var films = episodes.SelectMany(episode => episode.Movies).Distinct().ToList();
        if (films.Count is 1)
            return films[0];

        var shared = films
            .Select(film => (IEnumerable<MetadataGuid>)collectionsOf(film))
            .Aggregate((left, right) => left.Intersect(right))
            .Distinct()
            .Take(2)
            .ToList();
        return shared is [var collection] ? collection : null;
    }

    #endregion
}
