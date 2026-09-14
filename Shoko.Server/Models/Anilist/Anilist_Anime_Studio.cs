using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Anilist;

/// <summary>
/// AniList Anime ↔ Studio relationship, exposed as the studio in the context
/// of the anime.
/// </summary>
public class Anilist_Anime_Studio : IStudio<Anilist_Anime>
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int Anilist_Anime_StudioID { get; set; }

    /// <summary>
    /// AniList Anime ID.
    /// </summary>
    public int AnilistAnimeID { get; set; }

    /// <summary>
    /// AniList Studio ID.
    /// </summary>
    public int AnilistStudioID { get; set; }

    /// <summary>
    /// Whether this is the main studio for the anime.
    /// </summary>
    public bool IsMainStudio { get; set; }

    #endregion

    #region Constructors

    /// <summary>
    /// Default constructor for NHibernate.
    /// </summary>
    public Anilist_Anime_Studio() { }

    /// <summary>
    /// Creates a new anime-studio relationship.
    /// </summary>
    /// <param name="anilistAnimeId">The AniList anime ID.</param>
    /// <param name="anilistStudioId">The AniList studio ID.</param>
    /// <param name="isMainStudio">Whether this is the main studio.</param>
    public Anilist_Anime_Studio(int anilistAnimeId, int anilistStudioId, bool isMainStudio = false)
    {
        AnilistAnimeID = anilistAnimeId;
        AnilistStudioID = anilistStudioId;
        IsMainStudio = isMainStudio;
    }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Gets the associated AniList studio.
    /// </summary>
    public Anilist_Studio? Studio
        => RepoFactory.Anilist_Studio.GetByAnilistStudioID(AnilistStudioID);

    /// <summary>
    /// Gets the associated AniList anime.
    /// </summary>
    public Anilist_Anime? Anime
        => RepoFactory.Anilist_Anime.GetByAnilistAnimeID(AnilistAnimeID);

    #endregion

    #region IMetadata Implementation

    int IMetadata<int>.ID => AnilistStudioID;

    DataEntityType IMetadata.EntityType => DataEntityType.Studio;

    DataSource IMetadata.Source => DataSource.AniList;

    #endregion

    #region IStudio Implementation

    string IStudio.Name => Studio?.Name ?? string.Empty;

    string? IStudio.OriginalName => null;

    StudioType IStudio.StudioType => Studio?.IsAnimationStudio == true ? StudioType.Animation : StudioType.None;

    IEnumerable<IMovie> IStudio.MovieWorks => [];

    IEnumerable<ISeries> IStudio.SeriesWorks => Anime is { } anime ? [anime] : [];

    IEnumerable<IMetadata> IStudio.Works => Anime is { } anime ? [anime] : [];

    int IStudio<Anilist_Anime>.ParentID => AnilistAnimeID;

    Anilist_Anime? IStudio<Anilist_Anime>.Parent => Anime;

    #endregion
}
