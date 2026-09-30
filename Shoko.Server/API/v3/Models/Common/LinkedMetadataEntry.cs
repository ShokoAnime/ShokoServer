using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Server.API.Converters;
using Shoko.Server.API.v3.Helpers;

namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// A generic view of an entry a Shoko series or episode is linked to on a
/// metadata source the API has no model of its own for: its ID, titles,
/// overview, dates, rating and images, built from the abstractions alike
/// for every source.
/// </summary>
public class LinkedMetadataEntry
{
    #region Properties

    /// <summary>
    /// The source's own ID for the entry.
    /// </summary>
    [Required]
    public string ID { get; init; }

    /// <summary>
    /// What kind of entry it is: a show, a movie or an episode.
    /// </summary>
    [Required]
    public MetadataEntityType Type { get; init; }

    /// <summary>
    /// The preferred title.
    /// </summary>
    [Required]
    public string Title { get; init; }

    /// <summary>
    /// Every title the source gives the entry, preferred and default first.
    /// </summary>
    [Required]
    public IReadOnlyList<Title> Titles { get; init; }

    /// <summary>
    /// The preferred overview, or an empty string.
    /// </summary>
    [Required]
    public string Overview { get; init; }

    /// <summary>
    /// When the entry first aired or was released, if known.
    /// </summary>
    public PartialDateOnly? AirDate { get; init; }

    /// <summary>
    /// When a show last aired, if known. Left out for anything else.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public PartialDateOnly? EndDate { get; init; }

    /// <summary>
    /// An episode's season number, if the source has seasons. Left out for
    /// anything else.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? SeasonNumber { get; init; }

    /// <summary>
    /// An episode's number. Left out for anything else.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? EpisodeNumber { get; init; }

    /// <summary>
    /// The rating the source's users give the entry, on a scale of 1 to 10,
    /// or <c>null</c> when the source has none.
    /// </summary>
    public Rating? Rating { get; init; }

    /// <summary>
    /// The preferred image of each type.
    /// </summary>
    [Required]
    public Images Images { get; init; }

    #endregion

    #region Constructors

    /// <summary>
    /// Builds the view of a linked series.
    /// </summary>
    /// <param name="series">The series.</param>
    public LinkedMetadataEntry(ISeries series)
        : this(series, series, series.Rating, series.RatingVotes)
    {
        AirDate = series.AirDate;
        EndDate = series.EndDate;
    }

    /// <summary>
    /// Builds the view of a linked movie.
    /// </summary>
    /// <param name="movie">The movie.</param>
    public LinkedMetadataEntry(IMovie movie)
        : this(movie, movie, movie.Rating, movie.RatingVotes)
    {
        AirDate = PartialDateOnly.FromDateTime(movie.ReleaseDate);
    }

    /// <summary>
    /// Builds the view of a linked episode.
    /// </summary>
    /// <param name="episode">The episode.</param>
    public LinkedMetadataEntry(IEpisode episode)
        : this(episode, episode, episode.Rating, episode.RatingVotes)
    {
        AirDate = PartialDateOnly.FromDateOnly(episode.AirDate);
        SeasonNumber = episode.SeasonNumber;
        EpisodeNumber = episode.EpisodeNumber;
    }

    private LinkedMetadataEntry(IMetadata entry, IWithImages images, double rating, int votes)
    {
        var source = LegacyMetadataSpellings.Of(entry.Source);
        var titled = (IWithTitles)entry;
        var described = (IWithOverviews)entry;
        ID = entry.ID.ID;
        Type = entry.EntityType;
        Title = titled.Title;
        Titles = titled.Titles
            .Select(title => new Title(title, null, titled.PreferredTitle)
            {
                Type = title.Type,
                Default = ITitle.Equals(title, titled.DefaultTitle),
                Preferred = ITitle.Equals(title, titled.PreferredTitle),
                Source = source,
            })
            .OrderByDescending(title => title.Preferred)
            .ThenByDescending(title => title.Default)
            .ThenBy(title => title.Language)
            .ToList();
        Overview = described.PreferredOverview?.Value ?? described.DefaultOverview?.Value ?? string.Empty;
        Rating = rating > 0 || votes > 0
            ? new() { Value = rating, MaxValue = 10, Votes = votes, Source = source }
            : null;
        Images = images.GetImages(new() { IsEnabled = true, IsDesired = true }).ToDto(preferredImages: true);
    }

    #endregion
}
