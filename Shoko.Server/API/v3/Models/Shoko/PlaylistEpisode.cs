using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.AniDB;
using Shoko.Server.API.v3.Models.Common;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
/// A playlist episode entry without file data. Use <see cref="PlaylistItem.Parts"/>
/// for the associated video files.
/// </summary>
public class PlaylistEpisode
{
    /// <summary>
    /// Initializes a new <see cref="PlaylistEpisode"/>.
    /// </summary>
    /// <param name="shokoEpisode">Optional Shoko episode for local data.</param>
    /// <param name="shokoSeries">Optional Shoko series for local data.</param>
    /// <param name="anidbEpisode">AniDB episode metadata.</param>
    /// <param name="anidbAnime">AniDB anime metadata.</param>
    public PlaylistEpisode(IShokoEpisode shokoEpisode, IShokoSeries shokoSeries, IAnidbEpisode anidbEpisode, IAnidbAnime anidbAnime)
    {
        var tmdbShow = shokoSeries.TmdbShowCrossReferences.FirstOrDefault()?.TmdbShow;
        var tmdbMovie = shokoEpisode.TmdbMovieCrossReferences.FirstOrDefault()?.TmdbMovie;
        IDs = new PlaylistEpisodeIDs
        {
            ID = anidbEpisode.ID,
            Series = anidbAnime.ID,
            ShokoSeries = shokoSeries.ID,
            ShokoEpisode = shokoEpisode.ID,
            TmdbShow = tmdbShow?.ID,
            TmdbMovie = tmdbMovie?.ID,
            TvdbShow = tmdbShow?.TvdbShowID,
            ImdbMovie = tmdbMovie?.ImdbMovieID
        };
        Title = shokoEpisode.Title;
        Number = anidbEpisode.EpisodeNumber;
        Type = anidbEpisode.Type.ToV3Dto();
        AirDate = anidbEpisode.AirDate;
        SeriesTitle = shokoSeries.Title;
        SeriesPoster = shokoSeries.PrimaryImage is { } poster
            ? new Image(poster) : null;
        Thumbnail = shokoEpisode?.BackdropImage is { } thumbnail
            ? new Image(thumbnail) : null;
    }

    /// <summary>
    /// All ids that may be useful for navigating.
    /// </summary>
    [Required]
    public PlaylistEpisodeIDs IDs { get; set; }

    /// <summary>
    /// Episode title.
    /// </summary>
    [Required]
    public string Title { get; set; }

    /// <summary>
    /// Episode number.
    /// </summary>
    [Required]
    public int Number { get; set; }

    /// <summary>
    /// Episode type.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public EpisodeType Type { get; set; }

    /// <summary>
    /// Air date.
    /// </summary>
    public DateOnly? AirDate { get; set; }

    /// <summary>
    /// Series title.
    /// </summary>
    [Required]
    public string SeriesTitle { get; set; }

    /// <summary>
    /// Series poster.
    /// </summary>
    public Image? SeriesPoster { get; set; }

    /// <summary>
    /// Episode thumbnail.
    /// </summary>
    public Image? Thumbnail { get; set; }
}

/// <summary>
/// IDs for a <see cref="PlaylistEpisode"/>.
/// </summary>
public class PlaylistEpisodeIDs : IDs
{
    /// <summary>
    /// The related <see cref="AnidbEpisode"/> id.
    /// </summary>
    [Required]
    public new int ID { get; set; }

    /// <summary>
    /// The related <see cref="Series.AniDB"/> id.
    /// </summary>
    [Required]
    public int Series { get; set; }

    /// <summary>
    /// The related Shoko <see cref="Shoko.Episode"/> id, if available locally.
    /// </summary>
    [Required]
    public int ShokoEpisode { get; set; }

    /// <summary>
    /// The related Shoko <see cref="Shoko.Series"/> id, if available locally.
    /// </summary>
    [Required]
    public int ShokoSeries { get; set; }

    /// <summary>
    /// The first TMDB show id linked to the episode's series.
    /// </summary>
    public int? TmdbShow { get; set; }

    /// <summary>
    /// The first TMDB movie id linked to the episode.
    /// </summary>
    public int? TmdbMovie { get; set; }

    /// <summary>
    /// The TvDB show id linked to the first TMDB show linked to the episode's
    /// series.
    /// </summary>
    public int? TvdbShow { get; set; }

    /// <summary>
    /// The IMDB movie id linked to the first TMDB movie linked to the episode.
    /// </summary>
    public string? ImdbMovie { get; set; }
}
