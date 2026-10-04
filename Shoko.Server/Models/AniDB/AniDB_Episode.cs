using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Video;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB.Embedded;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

using EpisodeType = Shoko.Abstractions.Metadata.Enums.EpisodeType;

#pragma warning disable CS0618
namespace Shoko.Server.Models.AniDB;

public class AniDB_Episode : IEpisode, IAnidbEpisode, IInlineTextSource
{
    #region DB columns

    public int AniDB_EpisodeID { get; set; }

    public int EpisodeID { get; set; }

    public int AnimeID { get; set; }

    public int LengthSeconds { get; set; }

    public string Rating { get; set; } = "0";

    public double RatingDouble => double.TryParse(Rating, out var rating) ? rating : 0;

    public string Votes { get; set; } = "0";

    public int VotesInt => int.TryParse(Votes, out var votes) ? votes : 0;

    public int EpisodeNumber { get; set; }

    public EpisodeType EpisodeType { get; set; }

    public string Description { get; set; } = string.Empty;

    public int AirDate { get; set; }

    /// <summary>
    ///   When the episode was first stored locally. Set once and never changed.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    public DateTime DateTimeUpdated { get; set; }

    /// <summary>
    ///   Whether a user hid the episode. Set through
    ///   <see cref="IMetadataOrderingService.SetEpisodeHidden"/>.
    /// </summary>
    public bool IsHidden { get; set; }

    #endregion

    public TimeSpan Runtime => TimeSpan.FromSeconds(LengthSeconds);

    public string Title => (PreferredTitle ?? DefaultTitle).Value;

    /// <summary>
    ///   The episode's default title: its first English title, else a generic
    ///   one synthesized in the episode naming languages, as generic titles are
    ///   not stored.
    /// </summary>
    public ITitle DefaultTitle
        => GetTitles(TitleLanguage.English).FirstOrDefault() ?? AnidbText.SynthesizedTitle(EpisodeType, EpisodeNumber);

    /// <summary>
    ///   The episode's English title, as AniDB names it: its first English
    ///   title, else AniDB's generic one, <c>Episode {prefix}{number}</c>.
    /// </summary>
    public string EnglishTitle
        => GetTitles(TitleLanguage.English).FirstOrDefault()?.Value ?? AnidbText.GenericEnglishValue(EpisodeType, EpisodeNumber);

    /// <summary>
    ///   The title the user's picks and language settings choose for the
    ///   episode, or <c>null</c> when none is picked or in a preferred language.
    /// </summary>
    public ITitle? PreferredTitle => GetPreferredTitle(false);

    /// <summary>
    ///   The title the user's picks and language settings choose for the
    ///   episode.
    /// </summary>
    /// <param name="useFallback">Whether to fall back to the default title.</param>
    /// <returns>The title, or <c>null</c> when none is chosen and there is no fallback.</returns>
    public ITitle? GetPreferredTitle(bool useFallback)
        => AnidbText.Present(TextAccess.Manager.PreferredTitleFor(this)) ?? (useFallback ? DefaultTitle : null);

    public DateTime? GetAirDateAsDate() => AniDBExtensions.GetAniDBDateAsDate(AirDate);

    public DateOnly? GetAirDateAsDateOnly() => AniDBExtensions.GetAniDBDateAsDateOnly(AirDate);

    public PartialDateOnly? GetAirDateAsPartialDateOnly() => AniDBExtensions.GetAniDBDateAsPartialDateOnly(AirDate);

    /// <summary>
    ///   The date of the episode's regular broadcast, for matching it against
    ///   other sources: the stored date, unless the note in the anime's
    ///   description moves the episode from an early showing onto the regular
    ///   run.
    /// </summary>
    public DateOnly? RegularAirDate => AniDB_Anime?.GetRegularAirDate(this) ?? GetAirDateAsDateOnly();

    public bool HasAired
    {
        get
        {
            if (AniDB_Anime is not { } anidbAnime) return false;
            var date = GetAirDateAsDate();
            if (date == null) return anidbAnime.GetFinishedAiring();

            return date.Value.ToLocalTime() < DateTime.Now;
        }
    }

    /// <summary>
    ///   The titles AniDB gave the episode, in AniDB's own order, without
    ///   generic ones such as <c>Episode 5</c>, which are not stored.
    /// </summary>
    /// <param name="language">Optional. Only the titles in this language.</param>
    /// <returns>The titles.</returns>
    public IReadOnlyList<ITitle> GetTitles(TitleLanguage? language = null)
    {
        var titles = AnidbText.Present(TextAccess.Manager.OwnTitlesOf(this));
        return language is { } wanted ? [.. titles.Where(title => title.Language == wanted)] : titles;
    }

    #region Shoko

    public AnimeSeries? AnimeSeries => RepoFactory.AnimeSeries.GetByAnimeID(AnimeID);

    public AnimeEpisode? AnimeEpisode => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(EpisodeID);

    #endregion

    #region AniDB

    public AniDB_Anime? AniDB_Anime => RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);

    #endregion

    #region IMetadata Implementation

    MetadataGuid IMetadata.ID => new(MetadataSource.AniDB, MetadataEntityType.Episode, EpisodeID.ToString());

    int IAnidbEpisode.AnidbID => EpisodeID;

    #endregion

    #region IWithTitles Implementation

    IReadOnlyList<ITitle> IWithTitles.Titles => AnidbText.Present(TextAccess.Manager.ListTitles(this));

    #endregion

    #region IInlineTextSource Implementation

    ITitle? IInlineTextSource.InlineTitle => null;

    IText? IInlineTextSource.InlineOverview => InlineText.Overview(MetadataSource.AniDB, Description, TitleLanguage.English, "en");

    #endregion

    #region IWithDescription Implementation

    IText? IWithOverviews.DefaultOverview => TextAccess.Manager.DefaultOverviewFor(this);

    IText? IWithOverviews.PreferredOverview => TextAccess.Manager.PreferredOverviewFor(this);

    // A missing description is still listed, empty, as it always was.
    IReadOnlyList<IText> IWithOverviews.Overviews => [
        ((IInlineTextSource)this).InlineOverview ?? new TextStub
        {
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = Description,
            Source = MetadataSource.AniDB,
        },
    ];

    #endregion

    #region IWithCreationDate Implementation

    DateTime IWithCreationDate.CreatedAt => CreatedAt.ToUniversalTime();

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => DateTimeUpdated.ToUniversalTime();

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => RepoFactory.AniDB_Anime_Character.GetByAnimeID(AnimeID)
        .SelectMany(xref =>
        {
            // We don't want borked cross-references to show up.
            if (xref.Character is not { } character)
                return [];

            // If a role don't have a creator then we still want it to show up.
            var creatorXrefs = xref.CreatorCrossReferences;
            if (creatorXrefs is { Count: 0 })
                return [new AniDB_Cast(xref, character, null, () => this)];

            return creatorXrefs
                .Select(x => new AniDB_Cast(xref, character, x.CreatorID, () => this));
        })
        .ToList();

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => RepoFactory.AniDB_Anime_Staff.GetByAnimeID(AnimeID)
        .Select(xref =>
        {
            // Hide studio and actor roles from crew members. Actors should show up as cast, and studios as studios.
            if (xref is { RoleType: CreatorRoleType.Studio or CreatorRoleType.Actor })
                return null;

            return new AniDB_Crew(xref, () => this);
        })
        .WhereNotNull()
        .ToList();

    #endregion

    #region IWithResources Implementation

    IReadOnlyList<Resource> IWithResources.Resources
        => [.. GetAnidbResources(), .. ISystemService.StaticServices.GetRequiredService<IMetadataService>().GatherResourcesForEntity(this)];

    /// <summary>
    ///   Every resource AniDB lists for the episode, in AniDB's order.
    /// </summary>
    /// <returns>The episode's own links.</returns>
    public List<Resource> GetAnidbResources()
        => AnidbResourceLinks.ToResources(RepoFactory.AniDB_Resource.GetByEpisodeID(EpisodeID));

    #endregion

    #region IWithCrossSources Implementation

    IReadOnlyList<MetadataGuid> IWithCrossSources.CrossSourceIDs => [];

    #endregion

    #region IEpisode Implementation

    DateTime? IEpisode.LastRefreshedAt => (AniDB_Anime as ISeries)?.LastRefreshedAt;

    IReadOnlyList<IEpisodeOrderingInformation<IAnidbAnime, IAnidbEpisode>> IEpisode<IAnidbAnime, IAnidbEpisode>.Orderings => OrderingLookup.PlacesOf<IAnidbAnime, IAnidbEpisode>(this);

    IEpisodeOrderingInformation<IAnidbAnime, IAnidbEpisode>? IEpisode<IAnidbAnime, IAnidbEpisode>.PreferredOrdering => OrderingLookup.PreferredPlaceOf<IAnidbAnime, IAnidbEpisode>(this);

    IEpisodeOrderingInformation<IAnidbAnime, IAnidbEpisode> IEpisode<IAnidbAnime, IAnidbEpisode>.CurrentOrdering => OrderingLookup.DefaultPlaceOf<IAnidbAnime, IAnidbEpisode>(this);

    ISeries IEpisode.Series => Series;

    ISeason? IEpisode.Season => ((IEpisode<IAnidbAnime, IAnidbEpisode>)this).Season;

    IReadOnlyList<IEpisodeOrderingInformation> IEpisode.Orderings => ((IEpisode<IAnidbAnime, IAnidbEpisode>)this).Orderings;

    IEpisodeOrderingInformation? IEpisode.PreferredOrdering => ((IEpisode<IAnidbAnime, IAnidbEpisode>)this).PreferredOrdering;

    IEpisodeOrderingInformation IEpisode.CurrentOrdering => ((IEpisode<IAnidbAnime, IAnidbEpisode>)this).CurrentOrdering;

    IReadOnlyList<IMetadataEpisodeCrossReference> IEpisode.MetadataEpisodeCrossReferences =>
        ISystemService.StaticServices.GetService<IMetadataService>()?.GetEpisodeCrossReferences(EpisodeID) ?? [];

    IReadOnlyList<IMetadataSeriesCrossReference> IEpisode.MetadataSeriesCrossReferences =>
        ISystemService.StaticServices.GetService<IMetadataService>()?.GetSeriesCrossReferences(AnimeID) ?? [];

    IReadOnlyList<IMetadataMovieCrossReference> IEpisode.MetadataMovieCrossReferences =>
        ISystemService.StaticServices.GetService<IMetadataService>()?.GetMovieCrossReferences(EpisodeID) ?? [];

    int IAnidbEpisode.AnidbAnimeID => AnimeID;

    EpisodeType IEpisode.Type => EpisodeType;

    int? IEpisode.SeasonNumber => EpisodeType switch { EpisodeType.Episode => 1, EpisodeType.Special => 0, _ => null };

    MetadataGuid? IEpisode.SeasonID => ((IEpisode)this).SeasonNumber is { } seasonNumber
        ? new(MetadataSource.AniDB, MetadataEntityType.Season, AniDB_Season.GetID(AnimeID, EpisodeType, seasonNumber))
        : null;

    double IEpisode.Rating => RatingDouble;

    int IEpisode.RatingVotes => VotesInt;

    DateOnly? IEpisode.AirDate => GetAirDateAsDateOnly();

    DateTime? IEpisode.AirDateWithTime => GetAirDateAsDate();

    IReadOnlyList<IShokoEpisode> IEpisode.ShokoEpisodes => AnimeEpisode is IShokoEpisode shokoEpisode ? [shokoEpisode] : [];

    IReadOnlyList<IVideoCrossReference> IEpisode.VideoCrossReferences =>
        RepoFactory.CrossRef_File_Episode.GetByEpisodeID(EpisodeID);

    IReadOnlyList<IVideo> IEpisode.Videos =>
        RepoFactory.CrossRef_File_Episode.GetByEpisodeID(EpisodeID)
            .DistinctBy(xref => xref.Hash)
            .Select(xref => xref.VideoLocal)
            .WhereNotNull()
            .ToList();

    IReadOnlyList<int> IEpisode.ShokoEpisodeIDs => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(EpisodeID) is { } episode ? [episode.AnimeEpisodeID] : [];

    #endregion

    #region IEpisode<IAnidbAnime, IAnidbEpisode> Implementation

    public IAnidbAnime Series => AniDB_Anime ??
        throw new NullReferenceException($"Unable to find AniDB Anime {AnimeID} for AniDB Episode {EpisodeID}");

    ISeason<IAnidbAnime, IAnidbEpisode>? IEpisode<IAnidbAnime, IAnidbEpisode>.Season
        => ((IEpisode)this).SeasonID is { } seasonID && AniDB_Anime is { } anime
            ? anime.AniDBSeasons.FirstOrDefault(season => ((IMetadata)season).ID == seasonID)
            : null;

    #endregion
}
