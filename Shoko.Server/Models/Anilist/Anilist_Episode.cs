using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anilist;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Video;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Providers.Anilist;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Anilist;

/// <summary>
/// AniList Episode Database Model.
/// </summary>
/// <remarks>
/// AniList has no episode entity. Episodes are synthesized from the anime's
/// episode count, and the airing schedule entry is attached when one exists.
/// </remarks>
public class Anilist_Episode : Anilist_Base<int>, IAnilistEpisode
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int Anilist_EpisodeID { get; set; }

    /// <summary>
    /// Synthesized AniList Episode ID. See
    /// <see cref="AnilistUtility.PackEpisodeID"/>.
    /// </summary>
    public int AnilistEpisodeID { get; set; }

    /// <summary>
    /// AniList airing schedule ID, if the anime has a schedule entry for the
    /// episode.
    /// </summary>
    public int? AnilistScheduleEpisodeID { get; set; }

    /// <summary>
    /// AniList Anime ID.
    /// </summary>
    public int AnilistAnimeID { get; set; }

    /// <summary>
    /// Episode number.
    /// </summary>
    public int EpisodeNumber { get; set; }

    /// <summary>
    /// Episode runtime in minutes.
    /// </summary>
    public int? RuntimeMinutes { get; set; }

    /// <summary>
    /// When the episode aired, in UTC, from the airing schedule.
    /// </summary>
    public DateTime? AiredAt { get; set; }

    /// <summary>
    /// When the metadata was first downloaded.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the metadata was last synchronized.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Computed Properties

    /// <inheritdoc/>
    public override int Id => AnilistEpisodeID;

    /// <summary>
    /// Episode runtime as a TimeSpan.
    /// </summary>
    public TimeSpan Runtime
    {
        get => RuntimeMinutes.HasValue ? TimeSpan.FromMinutes(RuntimeMinutes.Value) : TimeSpan.Zero;
        set => RuntimeMinutes = (int)value.TotalMinutes;
    }

    /// <summary>
    /// The date the episode aired, without the time component.
    /// </summary>
    public DateOnly? AirDate => AiredAt is { } airedAt ? DateOnly.FromDateTime(airedAt) : null;

    /// <summary>
    /// Display title for the episode.
    /// </summary>
    public string Title => $"Episode {EpisodeNumber}";

    #endregion

    #region Constructors

    /// <summary>
    /// Default constructor for NHibernate.
    /// </summary>
    public Anilist_Episode() { }

    /// <summary>
    /// Creates a new AniList episode entry.
    /// </summary>
    /// <param name="anilistAnimeId">The AniList anime ID.</param>
    /// <param name="episodeNumber">The episode number.</param>
    public Anilist_Episode(int anilistAnimeId, int episodeNumber)
    {
        AnilistAnimeID = anilistAnimeId;
        EpisodeNumber = episodeNumber;
        AnilistEpisodeID = AnilistUtility.PackEpisodeID(anilistAnimeId, episodeNumber);
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Gets the associated AniList Anime.
    /// </summary>
    public Anilist_Anime? Anime
        => RepoFactory.Anilist_Anime.GetByAnilistAnimeID(AnilistAnimeID);

    /// <summary>
    /// Get all AniDB/AniList episode cross-references for the episode.
    /// </summary>
    public IReadOnlyList<CrossRef_AniDB_Anilist_Episode> CrossReferences
        => RepoFactory.CrossRef_AniDB_Anilist_Episode.GetByAnilistEpisodeID(AnilistEpisodeID);

    /// <summary>
    /// Get all file cross-references associated with the episode.
    /// </summary>
    public IReadOnlyList<CrossRef_File_Episode> FileCrossReferences
        => CrossReferences
            .DistinctBy(xref => xref.AnidbEpisodeID)
            .SelectMany(xref => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(xref.AnidbEpisodeID))
            .WhereNotNull()
            .ToList();

    #endregion

    #region IMetadata Implementation

    int IMetadata<int>.ID => AnilistEpisodeID;

    DataEntityType IMetadata.EntityType => DataEntityType.Episode;

    DataSource IMetadata.Source => DataSource.AniList;

    #endregion

    #region IEpisode Implementation

    int IEpisode.SeriesID => AnilistAnimeID;

    IReadOnlyList<int> IEpisode.ShokoEpisodeIDs => CrossReferences
        .Select(xref => xref.AnimeEpisode?.AnimeEpisodeID)
        .WhereNotNull()
        .Distinct()
        .ToList();

    EpisodeType IEpisode.Type => EpisodeType.Episode;

    int? IEpisode.SeasonNumber => 1;

    double IEpisode.Rating => 0;

    int IEpisode.RatingVotes => 0;

    TimeSpan IEpisode.Runtime => Runtime;

    DateOnly? IEpisode.AirDate => AirDate;

    DateTime? IEpisode.AirDateWithTime => AiredAt;

    ISeries? IEpisode.Series => Anime;

    IAnilistAnime IAnilistEpisode.Series => Anime ??
        throw new NullReferenceException($"Unable to find AniList anime with id {AnilistAnimeID} in IAnilistEpisode.Series");

    IReadOnlyList<IShokoEpisode> IEpisode.ShokoEpisodes => CrossReferences
        .Select(xref => xref.AnimeEpisode)
        .WhereNotNull()
        .DistinctBy(episode => episode.AnimeEpisodeID)
        .ToList();

    IReadOnlyList<IVideoCrossReference> IEpisode.CrossReferences => CrossReferences
        .DistinctBy(xref => xref.AnidbEpisodeID)
        .SelectMany(xref => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(xref.AnidbEpisodeID))
        .ToList();

    IReadOnlyList<IVideo> IEpisode.Videos => CrossReferences
        .DistinctBy(xref => xref.AnidbEpisodeID)
        .SelectMany(xref => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(xref.AnidbEpisodeID))
        .Select(xref => xref.VideoLocal)
        .WhereNotNull()
        .DistinctBy(video => video.VideoLocalID)
        .ToList();

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.Title => Title;

    ITitle IWithTitles.DefaultTitle => new TitleStub
    {
        Source = DataSource.AniList,
        Language = TitleLanguage.English,
        LanguageCode = "en",
        Value = Title,
        Type = TitleType.Main,
    };

    ITitle? IWithTitles.PreferredTitle => null;

    IReadOnlyList<ITitle> IWithTitles.Titles => [((IWithTitles)this).DefaultTitle];

    #endregion

    #region IWithDescriptions Implementation

    IText? IWithDescriptions.DefaultDescription => null;

    IText? IWithDescriptions.PreferredDescription => null;

    IReadOnlyList<IText> IWithDescriptions.Descriptions => [];

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => [];

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => [];

    #endregion

    #region IWithCreationDate Implementation

    DateTime IWithCreationDate.CreatedAt => CreatedAt;

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => LastUpdatedAt;

    #endregion
}
