using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   One section of a season view: which anime it takes, by type, by when
///   they started and by episode length. Each anime goes to the first
///   section of a layout that takes it.
/// </summary>
public sealed record SeasonSectionDefinition
{
    #region Constants

    /// <summary>
    ///   Episodes shorter than this are half length.
    /// </summary>
    public static readonly TimeSpan HalfLengthLimit = TimeSpan.FromMinutes(16);

    /// <summary>
    ///   The default layout: new full-length TV and web series, new
    ///   half-length ones, continuing ones, movies, and OVAs and TV
    ///   specials. TV shorts, music videos and anime of any other or unknown
    ///   type fit no section, so they are left out.
    /// </summary>
    public static IReadOnlyList<SeasonSectionDefinition> DefaultLayout { get; } =
    [
        new()
        {
            Title = "TV & Web",
            Types = new[] { AnimeType.TV, AnimeType.Web }.ToFrozenSet(),
            Continuing = false,
            HalfLength = false,
        },
        new()
        {
            Title = "TV & Web (Half Length)",
            Types = new[] { AnimeType.TV, AnimeType.Web }.ToFrozenSet(),
            Continuing = false,
            HalfLength = true,
        },
        new()
        {
            Title = "Continuing",
            Types = new[] { AnimeType.TV, AnimeType.Web }.ToFrozenSet(),
            Continuing = true,
        },
        new()
        {
            Title = "Movies",
            Types = new[] { AnimeType.Movie }.ToFrozenSet(),
        },
        new()
        {
            Title = "OVAs & Specials",
            Types = new[] { AnimeType.OVA, AnimeType.TVSpecial }.ToFrozenSet(),
        },
    ];

    #endregion

    #region Properties

    /// <summary>
    ///   The section's heading.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    ///   The anime types it takes, or <c>null</c> for the rest group: every
    ///   type, so whatever no earlier section took.
    /// </summary>
    public IReadOnlySet<AnimeType>? Types { get; init; }

    /// <summary>
    ///   <c>true</c> for only the anime that started before the viewed
    ///   season, <c>false</c> for only the new ones, <c>null</c> for both. An
    ///   anime with no known start counts as new.
    /// </summary>
    public bool? Continuing { get; init; }

    /// <summary>
    ///   <c>true</c> for only the anime whose episodes run shorter than
    ///   <see cref="HalfLengthLimit"/>, <c>false</c> for only the others,
    ///   <c>null</c> for both. An anime with no known length is full length.
    /// </summary>
    public bool? HalfLength { get; init; }

    #endregion
}
