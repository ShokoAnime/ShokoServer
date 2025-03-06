using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using TMDbLib.Objects.TvShows;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// The Movie DataBase (TMDB) Season Database Model.
/// </summary>
public class TMDB_Season : TMDB_Base<int>, IEntityMetadata, IMetadata<int>
{
    /// <summary>
    /// IEntityMetadata.Id
    /// </summary>
    public override int Id => TmdbSeasonID;

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_SeasonID { get; set; }

    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Season ID.
    /// </summary>
    public int TmdbSeasonID { get; set; }

    /// <summary>
    /// The default poster path. Used to determine the default poster for the show.
    /// </summary>
    public string PosterPath { get; set; } = string.Empty;

    /// <summary>
    /// The english title of the season, used as a fallback for when no title
    /// is available in the preferred language.
    /// </summary>
    public string EnglishTitle { get; set; } = string.Empty;

    /// <summary>
    /// The english overview, used as a fallback for when no overview is
    /// available in the preferred language.
    /// </summary>
    public string EnglishOverview { get; set; } = string.Empty;

    /// <summary>
    /// Number of episodes within the season.
    /// </summary>
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Number of episodes within the season that are hidden.
    /// </summary>
    public int HiddenEpisodeCount { get; set; }

    /// <summary>
    /// Season number for default ordering.
    /// </summary>
    public int SeasonNumber { get; set; }

    /// <summary>
    /// When the metadata was first downloaded.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the metadata was last synchronized with the remote.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>
    /// Get all titles for the season
    /// </summary>
    public virtual ICollection<TMDB_Title> AllTitles { get; set; }

    /// <summary>
    /// Get all overviews for the season
    /// </summary>
    public virtual ICollection<TMDB_Overview> AllOverviews { get; set; }

    /// <summary>
    /// Gets all image xrefs, which can be used to get all images for the season
    /// </summary>
    public virtual ICollection<TMDB_Image_Movie> ImageXRefs { get; set; }

    /// <summary>
    /// Get the TMDB show associated with the season, or null if the show have
    /// been purged from the local database for whatever reason.
    /// </summary>
    /// <returns>The TMDB show, or null.</returns>
    public virtual TMDB_Show? Show { get; set; }

    /// <summary>
    /// Get all local TMDB episodes associated with the season, or an empty list
    /// if the season have been purged from the local database for whatever
    /// reason.
    /// </summary>
    /// <returns>The TMDB episodes.</returns>
    public virtual ICollection<TMDB_Episode> Episodes { get; set; }

    /// <summary>
    /// Get all images for the movie
    /// </summary>
    [NotMapped]
    public IEnumerable<TMDB_Image> Images => ImageXRefs.OrderBy(a => a.ImageType).ThenBy(a => a.Ordering).Select(a => new
    {
        a.ImageType, Image = a.Image
    }).Where(a => a.Image != null).Select(a => new TMDB_Image
    {
        ImageType = a.ImageType,
        RemoteFileName = a.Image!.RemoteFileName,
        IsEnabled = a.Image.IsEnabled,
        IsPreferred = a.Image.IsPreferred,
        LanguageCode = a.Image.LanguageCode,
        Height = a.Image.Height,
        Width = a.Image.Width,
        TMDB_ImageID = a.Image.TMDB_ImageID,
        UserRating = a.Image.UserRating,
        UserVotes = a.Image.UserVotes
    }).ToList();

    [NotMapped]
    public TMDB_Image? DefaultPoster => Images.FirstOrDefault(a => a is { IsPreferred: true, ImageType: ImageEntityType.Poster });

    /// <summary>
    /// Get all cast members that have worked on this season.
    /// </summary>
    /// <returns>All cast members that have worked on this season.</returns>
    [NotMapped]
    public IReadOnlyList<TMDB_Season_Cast> Cast => Episodes.SelectMany(a => a.Cast)
            .GroupBy(cast => new { cast.TmdbPersonID, cast.CharacterName, cast.IsGuestRole })
            .Select(group =>
            {
                var episodes = group.ToList();
                var firstEpisode = episodes.First();
                return new TMDB_Season_Cast()
                {
                    TmdbPersonID = firstEpisode.TmdbPersonID,
                    TmdbShowID = firstEpisode.TmdbShowID,
                    TmdbSeasonID = firstEpisode.TmdbSeasonID,
                    IsGuestRole = firstEpisode.IsGuestRole,
                    CharacterName = firstEpisode.CharacterName,
                    Ordering = firstEpisode.Ordering,
                    EpisodeCount = episodes.Count,
                };
            })
            .OrderBy(crew => crew.Ordering)
            .ThenBy(crew => crew.TmdbPersonID)
            .ToList();

    /// <summary>
    /// Get all crew members that have worked on this season.
    /// </summary>
    /// <returns>All crew members that have worked on this season.</returns>
    [NotMapped]
    public IReadOnlyList<TMDB_Season_Crew> Crew => Episodes.SelectMany(a => a.Crew)
            .GroupBy(cast => new { cast.TmdbPersonID, cast.Department, cast.Job })
            .Select(group =>
            {
                var episodes = group.ToList();
                var firstEpisode = episodes.First();
                return new TMDB_Season_Crew()
                {
                    TmdbPersonID = firstEpisode.TmdbPersonID,
                    TmdbShowID = firstEpisode.TmdbShowID,
                    TmdbSeasonID = firstEpisode.TmdbSeasonID,
                    Department = firstEpisode.Department,
                    Job = firstEpisode.Job,
                    EpisodeCount = episodes.Count,
                };
            })
            .OrderBy(crew => crew.Department)
            .ThenBy(crew => crew.Job)
            .ThenBy(crew => crew.TmdbPersonID)
            .ToList();

    /// <summary>
    /// Populate the fields from the raw data.
    /// </summary>
    /// <param name="show">The raw TMDB Tv Show object.</param>
    /// <param name="season">The raw TMDB Tv Season object.</param>
    /// <returns>True if any of the fields have been updated.</returns>
    public bool Populate(TvShow show, TvSeason season)
    {
        var translation = season.Translations.Translations.FirstOrDefault(translation => translation.Iso_639_1 == "en");

        var updates = new[]
        {
            UpdateProperty(TmdbSeasonID, season.Id!.Value, v => TmdbSeasonID = v),
            UpdateProperty(TmdbShowID, show.Id, v => TmdbShowID = v),
            UpdateProperty(PosterPath, season.PosterPath, v => PosterPath = v),
            UpdateProperty(EnglishTitle, !string.IsNullOrEmpty(translation?.Data.Name) ? translation.Data.Name : season.Name, v => EnglishTitle = v),
            UpdateProperty(EnglishOverview, !string.IsNullOrEmpty(translation?.Data.Overview) ? translation.Data.Overview : season.Overview, v => EnglishOverview = v),
            UpdateProperty(SeasonNumber, season.SeasonNumber, v => SeasonNumber = v),
        };

        return updates.Any(updated => updated);
    }

    /// <summary>
    /// Get the preferred title using the preferred series title preference
    /// from the application settings.
    /// </summary>
    /// <param name="useFallback">Use a fallback title if no title was found in
    /// any of the preferred languages.</param>
    /// <returns>The preferred season title, or null if no preferred title was
    /// found.</returns>
    public TMDB_Title? GetPreferredTitle(bool useFallback = true)
    {
        var titles = AllTitles;

        foreach (var preferredLanguage in Languages.PreferredNamingLanguages)
        {
            if (preferredLanguage.Language == TitleLanguage.Main)
                return new TMDB_Title_Season { ParentID = TmdbSeasonID, Value = EnglishTitle, LanguageCode = "en", CountryCode = "US"};

            var title = titles.GetByLanguage(preferredLanguage.Language);
            if (title != null)
                return title;
        }

        return useFallback ? new TMDB_Title_Season { ParentID = TmdbSeasonID, Value = EnglishTitle, LanguageCode = "en", CountryCode = "US"} : null;
    }

    public TMDB_Overview? GetPreferredOverview(bool useFallback = true)
    {
        var overviews = AllOverviews;

        foreach (var preferredLanguage in Languages.PreferredDescriptionNamingLanguages)
        {
            var overview = overviews.GetByLanguage(preferredLanguage.Language);
            if (overview != null)
                return overview;
        }

        return useFallback ? new TMDB_Overview_Season {ParentID = TmdbSeasonID, Value = EnglishOverview, LanguageCode = "en", CountryCode = "US"} : null;
    }

    #region IEntityMetadata Implementation

    ForeignEntityType IEntityMetadata.Type => ForeignEntityType.Season;

    DataSourceEnum IEntityMetadata.DataSource => DataSourceEnum.TMDB;

    string? IEntityMetadata.OriginalTitle => null;

    TitleLanguage? IEntityMetadata.OriginalLanguage => null;

    string? IEntityMetadata.OriginalLanguageCode => null;

    DateOnly? IEntityMetadata.ReleasedAt => null;

    #endregion

    #region IMetadata Implementation

    int IMetadata<int>.ID => TmdbSeasonID;

    DataSourceEnum IMetadata.Source => DataSourceEnum.TMDB;

    #endregion
}
