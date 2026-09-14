using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anilist;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Anilist;

/// <summary>
/// Junction table for Anilist Anime and Tag relationships.
/// Implements IAnilistTagForAnime for tag information specific to an anime.
/// </summary>
public class Anilist_Anime_Tag : IAnilistTagForAnime
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int Anilist_Anime_TagID { get; set; }

    /// <summary>
    /// Anilist Anime ID.
    /// </summary>
    public int AnilistAnimeID { get; set; }

    /// <summary>
    /// Anilist Tag ID.
    /// </summary>
    public int AnilistTagID { get; set; }

    /// <summary>
    /// How relevant the tag is to the anime (0-100).
    /// </summary>
    public int Weight { get; set; }

    /// <summary>
    /// Whether this tag is considered a spoiler for this specific anime.
    /// </summary>
    public bool IsLocalSpoiler { get; set; }

    #endregion

    #region Constructors

    /// <summary>
    /// Default constructor for NHibernate.
    /// </summary>
    public Anilist_Anime_Tag() { }

    /// <summary>
    /// Creates a new anime-tag relationship.
    /// </summary>
    /// <param name="anilistAnimeId">The Anilist anime ID.</param>
    /// <param name="anilistTagId">The Anilist tag ID.</param>
    /// <param name="weight">The relevance weight.</param>
    /// <param name="isLocalSpoiler">Whether it's a spoiler for this anime.</param>
    public Anilist_Anime_Tag(int anilistAnimeId, int anilistTagId, int weight = 0, bool isLocalSpoiler = false)
    {
        AnilistAnimeID = anilistAnimeId;
        AnilistTagID = anilistTagId;
        Weight = weight;
        IsLocalSpoiler = isLocalSpoiler;
    }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Gets the associated Anilist Tag.
    /// </summary>
    public Anilist_Tag? Tag
        => RepoFactory.Anilist_Tag.GetByAnilistTagID(AnilistTagID);

    /// <summary>
    /// Gets the associated Anilist Anime.
    /// </summary>
    public Anilist_Anime? Anime
        => RepoFactory.Anilist_Anime.GetByAnilistAnimeID(AnilistAnimeID);

    #endregion

    #region IAnilistTagForAnime Implementation

    int IAnilistTagForAnime.AnilistAnimeID => AnilistAnimeID;

    int IAnilistTagForAnime.Weight => Weight;

    bool IAnilistTagForAnime.IsLocalSpoiler => IsLocalSpoiler;

    IAnilistAnime IAnilistTagForAnime.AnilistAnime => Anime!;

    #endregion

    #region IAnilistTag Implementation

    string IAnilistTag.Category => Tag?.Category ?? string.Empty;

    bool IAnilistTag.IsRestricted => Tag?.IsRestricted ?? false;

    bool IAnilistTag.IsSpoiler => Tag?.IsSpoiler ?? false;

    IReadOnlyList<IAnilistAnime> IAnilistTag.AllAnilistAnime => [];

    #endregion

    #region ITag Implementation

    int IMetadata<int>.ID => AnilistTagID;

    DataSource IMetadata.Source => DataSource.AniList;

    string ITag.Name => Tag?.Name ?? string.Empty;

    string ITag.Description => Tag?.Description ?? string.Empty;

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => Tag?.LastUpdatedAt ?? DateTime.MinValue;

    #endregion
}
