using System;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;

#nullable enable
namespace Shoko.Server.API.v3.Models.Airing;

/// <summary>
/// The little a calendar card needs to know about the series an airing belongs
/// to. Use the series routes for everything else.
/// </summary>
/// <param name="series">The series.</param>
/// <exception cref="ArgumentNullException"><paramref name="series"/> is <c>null</c>.</exception>
public class AiringSeries(ISeries series)
{
    /// <summary>
    /// The ID of the shoko series, or <c>null</c> when the series is not in the
    /// collection.
    /// </summary>
    public int? ShokoID { get; init; } = (series as IShokoSeries)?.ID;

    /// <summary>
    /// The ID of the AniDB anime, or <c>null</c> when the series is not keyed
    /// on AniDB.
    /// </summary>
    public int? AnidbID { get; init; } = series is IShokoSeries shokoSeries
        ? shokoSeries.AnidbAnimeID
        : series.Source is DataSource.AniDB ? series.ID : null;

    /// <summary>
    /// The preferred title of the series.
    /// </summary>
    [Required]
    public string Title { get; init; } = series.Title;
}
