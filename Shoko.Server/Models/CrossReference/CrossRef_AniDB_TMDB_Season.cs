using System;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;

#pragma warning disable CS0618
#nullable enable
namespace Shoko.Server.Models.CrossReference;

/// <summary>
/// Not actually stored in the database, but made from the episode cross-reference.
/// </summary>
public class CrossRef_AniDB_TMDB_Season : IEquatable<CrossRef_AniDB_TMDB_Season>, ITmdbSeasonCrossReference
{
    #region Columns

    public int AnidbAnimeID { get; set; }

    public int TmdbShowID { get; set; }

    private readonly int _tmdbSeasonID;

    private readonly string? _tmdbEpisodeGroupCollectionID;

    public string TmdbSeasonID => IsAlternateSeason ? _tmdbEpisodeGroupCollectionID : _tmdbSeasonID.ToString();

    public int SeasonNumber { get; set; }

    [MemberNotNullWhen(true, nameof(_tmdbEpisodeGroupCollectionID))]
    public bool IsAlternateSeason => _tmdbSeasonID is < 1 && !string.IsNullOrEmpty(_tmdbEpisodeGroupCollectionID);

    #endregion

    #region Constructors

    public CrossRef_AniDB_TMDB_Season(int anidbAnimeId, int tmdbSeasonId, int tmdbShowId, int seasonNumber = 1)
    {
        AnidbAnimeID = anidbAnimeId;
        _tmdbSeasonID = tmdbSeasonId;
        TmdbShowID = tmdbShowId;
        SeasonNumber = seasonNumber;
    }

    public CrossRef_AniDB_TMDB_Season(int anidbAnimeId, string tmdbEpisodeGroupCollectionId, int tmdbShowId, int seasonNumber = 1)
    {
        AnidbAnimeID = anidbAnimeId;
        _tmdbEpisodeGroupCollectionID = tmdbEpisodeGroupCollectionId;
        TmdbShowID = tmdbShowId;
        SeasonNumber = seasonNumber;
    }

    #endregion
    #region Methods

    public AniDB_Anime? AnidbAnime =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnidbAnimeID);

    public AnimeSeries? AnimeSeries =>
        RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    public TMDB_Season? TmdbSeason =>
        IsAlternateSeason || _tmdbSeasonID is < 1 ? null : RepoFactory.TMDB_Season.GetByTmdbSeasonID(_tmdbSeasonID);

    public TMDB_AlternateOrdering_Season? TmdbAlternateOrderingSeason =>
        !IsAlternateSeason || string.IsNullOrEmpty(_tmdbEpisodeGroupCollectionID) ? null : RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(_tmdbEpisodeGroupCollectionID);

    public TMDB_Show? TmdbShow =>
        TmdbShowID == 0 ? null : RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID);

    public bool Equals(CrossRef_AniDB_TMDB_Season? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return AnidbAnimeID == other.AnidbAnimeID
               && TmdbSeasonID == other.TmdbSeasonID
               && TmdbShowID == other.TmdbShowID
               && SeasonNumber == other.SeasonNumber;
    }

    public override bool Equals(object? obj)
        => Equals(obj as CrossRef_AniDB_TMDB_Season);

    public override int GetHashCode()
        => HashCode.Combine(AnidbAnimeID, TmdbSeasonID, TmdbShowID, SeasonNumber);

    #endregion

    #region IMetadata Implementation

    DataSource IMetadata.Source => DataSource.TMDB;

    #endregion

    #region ITmdbSeasonCrossReference Implementation

    IShokoSeries? ITmdbSeasonCrossReference.ShokoSeries => AnimeSeries;

    ITmdbShow? ITmdbSeasonCrossReference.TmdbShow => TmdbShow;

    ITmdbSeason? ITmdbSeasonCrossReference.TmdbSeason => TmdbSeason;

    #endregion
}
