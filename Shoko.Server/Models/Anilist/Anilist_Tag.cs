using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anilist;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Anilist;

/// <summary>
/// Anilist Tag Database Model.
/// </summary>
public class Anilist_Tag : Anilist_Base<int>, IAnilistTag
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int Anilist_TagID { get; set; }

    /// <summary>
    /// Anilist Tag ID.
    /// </summary>
    public int AnilistTagID { get; set; }

    /// <summary>
    /// Tag name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tag description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Tag category.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Whether the tag is restricted (adult content).
    /// </summary>
    public bool IsRestricted { get; set; }

    /// <summary>
    /// Whether the tag is considered a spoiler globally.
    /// </summary>
    public bool IsSpoiler { get; set; }

    /// <summary>
    /// When the metadata was last synchronized.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Computed Properties

    /// <inheritdoc/>
    public override int Id => AnilistTagID;

    #endregion

    #region Constructors

    /// <summary>
    /// Default constructor for NHibernate.
    /// </summary>
    public Anilist_Tag() { }

    /// <summary>
    /// Creates a new Anilist tag entry.
    /// </summary>
    /// <param name="anilistTagId">The Anilist tag ID.</param>
    public Anilist_Tag(int anilistTagId)
    {
        AnilistTagID = anilistTagId;
        LastUpdatedAt = DateTime.Now;
    }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Gets every anime the tag is set on, with the tag's weight and spoiler
    /// flag for each.
    /// </summary>
    public IReadOnlyList<Anilist_Anime_Tag> AnimeTags
        => RepoFactory.Anilist_Anime_Tag.GetByAnilistTagID(AnilistTagID);

    #endregion

    #region IAnilistTag Implementation

    IReadOnlyList<IAnilistAnime> IAnilistTag.AllAnilistAnime
        => AnimeTags.Select(animeTag => animeTag.Anime).WhereNotNull().ToList();

    #endregion

    #region IMetadata Implementation

    int IMetadata<int>.ID => AnilistTagID;

    DataSource IMetadata.Source => DataSource.AniList;

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => LastUpdatedAt;

    #endregion
}
