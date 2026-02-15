using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;
using Shoko.Abstractions.Video;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_AlternateOrdering_Episode : TMDB_Base<string>, ITmdbEpisode, ITmdbEpisodeOrderingInformation
{
    #region Properties

    public override string Id => $"{TmdbEpisodeGroupID}:{TmdbEpisodeID}";

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_AlternateOrdering_EpisodeID { get; set; }

    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Episode Group Collection ID.
    /// </summary>
    public string TmdbEpisodeGroupCollectionID { get; set; } = string.Empty;

    /// <summary>
    /// TMDB Episode Group ID.
    /// </summary>
    public string TmdbEpisodeGroupID { get; set; } = string.Empty;

    /// <summary>
    /// TMDB Episode ID.
    /// </summary>
    public int TmdbEpisodeID { get; set; }

    /// <summary>
    /// Overridden season number for alternate ordering.
    /// </summary>
    public int SeasonNumber { get; set; }

    /// <summary>
    /// Overridden episode number for alternate ordering.
    /// </summary>
    /// <value></value>
    public int EpisodeNumber { get; set; }

    /// <summary>
    /// When the metadata was first downloaded.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the metadata was last synchronized with the remote.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion
    #region Constructors

    public TMDB_AlternateOrdering_Episode() { }

    public TMDB_AlternateOrdering_Episode(string episodeGroupId, int episodeId)
    {
        TmdbEpisodeGroupID = episodeGroupId;
        TmdbEpisodeID = episodeId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion
    #region Methods

    public bool Populate(string collectionId, int showId, int seasonNumber, int episodeNumber)
    {
        var updates = new[]
        {
            UpdateProperty(TmdbShowID, showId, v => TmdbShowID = v),
            UpdateProperty(TmdbEpisodeGroupCollectionID, collectionId, v => TmdbEpisodeGroupCollectionID = v),
            UpdateProperty(SeasonNumber, seasonNumber, v => SeasonNumber = v),
            UpdateProperty(EpisodeNumber, episodeNumber, v => EpisodeNumber = v),
        };

        return updates.Any(updated => updated);
    }

    public TMDB_Show? TmdbShow =>
        RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID);

    public TMDB_AlternateOrdering? TmdbAlternateOrdering =>
        RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(TmdbEpisodeGroupCollectionID);

    public TMDB_AlternateOrdering_Season? TmdbAlternateOrderingSeason =>
        RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(TmdbEpisodeGroupID);

    public TMDB_Episode? TmdbEpisode =>
        RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(TmdbEpisodeID);

    private TMDB_Episode? _tmdbEpisode;

    public ITmdbEpisode GetTmdbEpisode() =>
        _tmdbEpisode ??= RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(TmdbEpisodeID) ?? throw new Exception($"Unable to find TMDB_Episode with ID {TmdbEpisodeID}");

    #endregion

    #region IMetadata Implementation

    DataSource IMetadata.Source => DataSource.TMDB;

    int IMetadata<int>.ID => TmdbEpisodeID;

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.Title => GetTmdbEpisode().Title;

    ITitle IWithTitles.DefaultTitle => GetTmdbEpisode().DefaultTitle;

    ITitle? IWithTitles.PreferredTitle => GetTmdbEpisode().PreferredTitle;

    IReadOnlyList<ITitle> IWithTitles.Titles => GetTmdbEpisode().Titles;

    #endregion

    #region IWithDescriptions Implementation

    IText? IWithDescriptions.DefaultDescription => GetTmdbEpisode().DefaultDescription;

    IText? IWithDescriptions.PreferredDescription => GetTmdbEpisode().PreferredDescription;

    IReadOnlyList<IText> IWithDescriptions.Descriptions => GetTmdbEpisode().Descriptions;

    #endregion

    #region IWithCreationDate Implementation

    DateTime IWithCreationDate.CreatedAt => CreatedAt.ToUniversalTime();

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => LastUpdatedAt.ToUniversalTime();

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => GetTmdbEpisode().Cast;

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => GetTmdbEpisode().Crew;

    #endregion

    #region IWithImages Implementation

    IImage? IWithImages.GetPreferredImageForType(ImageEntityType entityType)
        => null;

    IReadOnlyList<IImage> IWithImages.GetImages(ImageEntityType? entityType)
        => entityType.HasValue
            ? RepoFactory.TMDB_Image.GetByTmdbEpisodeIDAndType(TmdbEpisodeID, entityType.Value)
            : RepoFactory.TMDB_Image.GetByTmdbEpisodeID(TmdbEpisodeID);

    #endregion

    #region IEpisode Implementation

    int IEpisode.SeriesID => TmdbShowID;

    IReadOnlyList<int> IEpisode.ShokoEpisodeIDs => GetTmdbEpisode().ShokoEpisodeIDs;

    EpisodeType IEpisode.Type => SeasonNumber == 0 ? EpisodeType.Special : EpisodeType.Episode;

    int IEpisode.EpisodeNumber => EpisodeNumber;

    int? IEpisode.SeasonNumber => SeasonNumber;

    double IEpisode.Rating => GetTmdbEpisode().Rating;

    int IEpisode.RatingVotes => GetTmdbEpisode().RatingVotes;

    IImage? IEpisode.DefaultThumbnail => GetTmdbEpisode().DefaultThumbnail;

    TimeSpan IEpisode.Runtime => GetTmdbEpisode().Runtime;

    DateOnly? IEpisode.AirDate => GetTmdbEpisode().AirDate;

    DateTime? IEpisode.AirDateWithTime => GetTmdbEpisode().AirDateWithTime;

    ISeries? IEpisode.Series => TmdbShow;

    IReadOnlyList<IShokoEpisode> IEpisode.ShokoEpisodes => GetTmdbEpisode().ShokoEpisodes;

    IReadOnlyList<IVideoCrossReference> IEpisode.CrossReferences => GetTmdbEpisode().CrossReferences;

    IReadOnlyList<IVideo> IEpisode.VideoList => GetTmdbEpisode().VideoList;

    #endregion

    #region ITmdbEpisode Implementation

    string ITmdbEpisode.SeasonID => TmdbEpisodeGroupID;

    string ITmdbEpisode.OrderingID => TmdbEpisodeGroupCollectionID;

    int? ITmdbEpisode.TvdbEpisodeID => GetTmdbEpisode().TvdbEpisodeID;

    ITmdbShow? ITmdbEpisode.Series => TmdbShow;

    bool ITmdbEpisode.IsHidden => GetTmdbEpisode().IsHidden;

    ITmdbSeason? ITmdbEpisode.Season => TmdbAlternateOrderingSeason;

    ITmdbEpisodeOrderingInformation ITmdbEpisode.Ordering => this;

    ITmdbShowOrderingInformation? ITmdbEpisode.SeriesOrdering => TmdbAlternateOrdering;

    ITmdbEpisodeOrderingInformation? ITmdbEpisode.PreferredOrdering =>
        (
            TmdbShow is not { } tmdbShow ||
            tmdbShow.PreferredAlternateOrderingID is not { Length: > 0 } ||
            tmdbShow.PreferredAlternateOrderingID == TmdbShowID.ToString()
        )
            ? this
            : RepoFactory.TMDB_AlternateOrdering_Episode.GetByEpisodeGroupCollectionAndEpisodeIDs(tmdbShow.PreferredAlternateOrderingID, TmdbEpisodeID);

    IReadOnlyList<ITmdbEpisodeOrderingInformation> ITmdbEpisode.AllOrderings => [this, .. RepoFactory.TMDB_AlternateOrdering_Episode.GetByTmdbEpisodeID(TmdbEpisodeID)];

    #endregion

    #region ITmdbEpisodeOrderingInformation Implementation

    int ITmdbEpisodeOrderingInformation.SeriesID => TmdbShowID;

    string ITmdbEpisodeOrderingInformation.OrderingID => TmdbEpisodeGroupCollectionID;

    string ITmdbEpisodeOrderingInformation.SeasonID => TmdbEpisodeGroupID;

    int ITmdbEpisodeOrderingInformation.EpisodeID => TmdbEpisodeID;

    bool ITmdbEpisodeOrderingInformation.IsDefault => true;

    bool ITmdbEpisodeOrderingInformation.IsPreferred =>
        TmdbShow is not { } tmdbShow ||
        tmdbShow.PreferredAlternateOrderingID is not { Length: > 0 } ||
        tmdbShow.PreferredAlternateOrderingID == TmdbShowID.ToString();

    ITmdbShow? ITmdbEpisodeOrderingInformation.Series => TmdbShow;

    ITmdbSeason? ITmdbEpisodeOrderingInformation.Season => TmdbAlternateOrderingSeason;

    ITmdbEpisode ITmdbEpisodeOrderingInformation.Episode => this;

    IReadOnlyList<ITmdbEpisodeCrossReference> ITmdbEpisode.TmdbEpisodeCrossReferences => GetTmdbEpisode().TmdbEpisodeCrossReferences;

    #endregion
}
