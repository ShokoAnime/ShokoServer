using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Anilist;

/// <summary>
/// AniList Studio Database Model.
/// </summary>
public class Anilist_Studio : Anilist_Base<int>, IStudio
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int Anilist_StudioID { get; set; }

    /// <summary>
    /// AniList Studio ID.
    /// </summary>
    public int AnilistStudioID { get; set; }

    /// <summary>
    /// Studio name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is an animation studio.
    /// </summary>
    public bool IsAnimationStudio { get; set; }

    /// <summary>
    /// Number of users that favorited the studio.
    /// </summary>
    public int FavoriteCount { get; set; }

    /// <summary>
    /// When the metadata was last synchronized.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Computed Properties

    /// <inheritdoc/>
    public override int Id => AnilistStudioID;

    #endregion

    #region Constructors

    /// <summary>
    /// Default constructor for NHibernate.
    /// </summary>
    public Anilist_Studio() { }

    /// <summary>
    /// Creates a new AniList studio entry.
    /// </summary>
    /// <param name="anilistStudioId">The AniList studio ID.</param>
    public Anilist_Studio(int anilistStudioId)
    {
        AnilistStudioID = anilistStudioId;
        LastUpdatedAt = DateTime.Now;
    }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Gets all anime-studio relationships for this studio.
    /// </summary>
    public IReadOnlyList<Anilist_Anime_Studio> AnimeStudios
        => RepoFactory.Anilist_Anime_Studio.GetByAnilistStudioID(AnilistStudioID);

    /// <summary>
    /// Gets all anime this studio worked on.
    /// </summary>
    public IReadOnlyList<Anilist_Anime> Anime
        => AnimeStudios.Select(x => x.Anime).WhereNotNull().ToList();

    #endregion

    #region IMetadata Implementation

    int IMetadata<int>.ID => AnilistStudioID;

    DataEntityType IMetadata.EntityType => DataEntityType.Studio;

    DataSource IMetadata.Source => DataSource.AniList;

    #endregion

    #region IStudio Implementation

    string IStudio.Name => Name;

    string? IStudio.OriginalName => null;

    StudioType IStudio.StudioType => IsAnimationStudio ? StudioType.Animation : StudioType.None;

    IEnumerable<IMovie> IStudio.MovieWorks => [];

    IEnumerable<ISeries> IStudio.SeriesWorks => Anime;

    IEnumerable<IMetadata> IStudio.Works => Anime;

    #endregion
}
