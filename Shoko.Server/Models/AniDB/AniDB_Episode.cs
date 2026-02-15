using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Video;
using Shoko.Server.Extensions;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_Episode : IEpisode, IAnidbEpisode
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

    public DateTime DateTimeUpdated { get; set; }

    #endregion

    public TimeSpan Runtime => TimeSpan.FromSeconds(LengthSeconds);

    public string Title => (PreferredTitle ?? DefaultTitle).Value;

    private ITitle? _defaultTitle;

    public ITitle DefaultTitle
    {
        get
        {
            if (_defaultTitle is not null)
                return _defaultTitle;

            lock (this)
            {
                if (_defaultTitle is not null)
                    return _defaultTitle;

                // Fallback to English if available.
                if (RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(AniDB_EpisodeID, TitleLanguage.English) is { Count: > 0 } titles)
                    return _defaultTitle = titles[0];

                return _defaultTitle = new TitleStub()
                {
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Value = $"<AniDB Episode {AniDB_EpisodeID}>",
                    Source = DataSource.None,
                };
            }
        }
    }

    public ITitle? PreferredTitle => GetPreferredTitle(false);

    public ITitle? GetPreferredTitle(bool useFallback)
    {
        // Try finding one of the preferred languages.
        foreach (var language in Languages.PreferredEpisodeNamingLanguages)
        {
            if (language.Language == TitleLanguage.Main)
                return DefaultTitle;

            var title = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(EpisodeID, language.Language)
                .FirstOrDefault();
            if (title is not null)
                return title;
        }

        // Fallback to English if available.
        return useFallback ? DefaultTitle : null;
    }

    public DateTime? GetAirDateAsDate() => Providers.AniDB.AniDBExtensions.GetAniDBDateAsDate(AirDate);

    public DateOnly? GetAirDateAsDateOnly() => Providers.AniDB.AniDBExtensions.GetAniDBDateAsDateOnly(AirDate);

    public bool HasAired
    {
        get
        {
            if (AniDB_Anime is not { } anidbAnime) return false;
            if (anidbAnime.GetFinishedAiring()) return true;
            var date = GetAirDateAsDate();
            if (date == null) return false;

            return date.Value.ToLocalTime() < DateTime.Now;
        }
    }

    public IReadOnlyList<AniDB_Episode_Title> GetTitles(TitleLanguage? language = null) => language.HasValue
        ? RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(EpisodeID, language.Value)
        : RepoFactory.AniDB_Episode_Title.GetByEpisodeID(EpisodeID);

    #region Images

    public IImage? PreferredOrDefaultThumbnail
    {
        get
        {
            if (TmdbEpisodeCrossReferences is { Count: > 0 } tmdbEpisodeCrossReferences)
                return GetPreferredImageForType(ImageEntityType.Thumbnail) ?? tmdbEpisodeCrossReferences[0].TmdbEpisode?.GetImages(ImageEntityType.Thumbnail).FirstOrDefault();

            if (TmdbMovieCrossReferences is { Count: > 0 } tmdbMovieCrossReferences)
                return GetPreferredImageForType(ImageEntityType.Backdrop) ?? tmdbMovieCrossReferences[0].TmdbMovie?.GetImages(ImageEntityType.Backdrop).FirstOrDefault();

            return null;
        }
    }

    public IImage? GetPreferredImageForType(ImageEntityType entityType)
        => RepoFactory.AniDB_Episode_PreferredImage.GetByAnidbEpisodeIDAndType(EpisodeID, entityType)?.GetImageMetadata();

    public IReadOnlyList<IImage> GetImages(ImageEntityType? entityType = null)
    {
        var preferredImages = (entityType.HasValue ? [RepoFactory.AniDB_Episode_PreferredImage.GetByAnidbEpisodeIDAndType(EpisodeID, entityType.Value)!] : RepoFactory.AniDB_Episode_PreferredImage.GetByEpisodeID(EpisodeID))
            .WhereNotNull()
            .Select(preferredImage => preferredImage.GetImageMetadata())
            .WhereNotNull()
            .ToDictionary(image => image.ImageType);
        var images = new List<IImage>();
        if (!entityType.HasValue || entityType.Value is ImageEntityType.Poster)
        {
            var poster = AniDB_Anime?.GetImageMetadata(false);
            if (poster is not null)
                images.Add(preferredImages.TryGetValue(ImageEntityType.Poster, out var preferredPoster) && poster.Equals(preferredPoster)
                    ? preferredPoster
                    : poster
                );
        }
        foreach (var tmdbEpisode in TmdbEpisodes)
            images.AddRange(tmdbEpisode.GetImages(entityType, preferredImages));
        foreach (var tmdbMovie in TmdbMovies)
            images.AddRange(tmdbMovie.GetImages(entityType, preferredImages));

        return images
            .DistinctBy(image => (image.ImageType, image.Source, image.ID))
            .ToList();
    }

    #endregion

    #region Shoko

    public AnimeSeries? AnimeSeries => RepoFactory.AnimeSeries.GetByAnimeID(AnimeID);

    public AnimeEpisode? AnimeEpisode => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(EpisodeID);

    #endregion

    #region AniDB

    public AniDB_Anime? AniDB_Anime => RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);

    #endregion

    #region TMDB

    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> TmdbMovieCrossReferences =>
        RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbEpisodeID(EpisodeID);

    public IReadOnlyList<TMDB_Movie> TmdbMovies =>
        TmdbMovieCrossReferences
            .Select(xref => xref.TmdbMovie)
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> TmdbEpisodeCrossReferences =>
        RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbEpisodeID(EpisodeID);

    public IReadOnlyList<TMDB_Episode> TmdbEpisodes =>
        TmdbEpisodeCrossReferences
            .Select(xref => xref.TmdbEpisode)
            .WhereNotNull()
            .ToList();

    #endregion

    #region IMetadata Implementation

    DataSource IMetadata.Source => DataSource.AniDB;

    int IMetadata<int>.ID => EpisodeID;

    #endregion

    #region IWithTitles Implementation

    IReadOnlyList<ITitle> IWithTitles.Titles => GetTitles();

    #endregion

    #region IWithDescription Implementation

    IText? IWithDescriptions.DefaultDescription => Description is { Length: > 0 }
        ? new TextStub()
        {
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = Description,
            Source = DataSource.AniDB,
        }
        : null;

    IText? IWithDescriptions.PreferredDescription => Description is { Length: > 0 } && Utils.SettingsProvider.GetSettings().Language.DescriptionLanguageOrder.Contains("en")
        ? new TextStub()
        {
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = Description,
            Source = DataSource.AniDB,
        }
        : null;

    IReadOnlyList<IText> IWithDescriptions.Descriptions => [
        new TextStub()
        {
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = Description,
            Source = DataSource.AniDB,
        },
    ];

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => DateTimeUpdated.ToUniversalTime();

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => RepoFactory.AniDB_Anime_Character.GetByAnimeID(AnimeID)
        .SelectMany(xref =>
        {
            // We don't want organizations or borked cross-references to show up.
            var character = xref.Character;
            if (character is not { Type: not Server.CharacterType.Organization })
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
            if (xref is { RoleType: Server.CreatorRoleType.Studio or Server.CreatorRoleType.Actor })
                return null;

            return new AniDB_Crew(xref, () => this);
        })
        .WhereNotNull()
        .ToList();

    #endregion

    #region IEpisode Implementation

    int IEpisode.SeriesID => AnimeID;

    EpisodeType IEpisode.Type => EpisodeType;

    int? IEpisode.SeasonNumber => EpisodeType switch { EpisodeType.Episode => 1, EpisodeType.Special => 0, _ => null };

    double IEpisode.Rating => RatingDouble;

    int IEpisode.RatingVotes => VotesInt;

    IImage? IEpisode.DefaultThumbnail => null;

    DateOnly? IEpisode.AirDate => GetAirDateAsDateOnly();

    DateTime? IEpisode.AirDateWithTime => GetAirDateAsDate();

    ISeries? IEpisode.Series => AniDB_Anime;

    IReadOnlyList<IShokoEpisode> IEpisode.ShokoEpisodes => AnimeEpisode is IShokoEpisode shokoEpisode ? [shokoEpisode] : [];

    IReadOnlyList<IVideoCrossReference> IEpisode.CrossReferences =>
        RepoFactory.CrossRef_File_Episode.GetByEpisodeID(EpisodeID);

    IReadOnlyList<IVideo> IEpisode.VideoList =>
        RepoFactory.CrossRef_File_Episode.GetByEpisodeID(EpisodeID)
            .DistinctBy(xref => xref.Hash)
            .Select(xref => xref.VideoLocal)
            .WhereNotNull()
            .ToList();

    IReadOnlyList<int> IEpisode.ShokoEpisodeIDs => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(EpisodeID) is { } episode ? [episode.AnimeEpisodeID] : [];

    #endregion

    #region IAnidbEpisode Implementation

    public IAnidbAnime? Series => AniDB_Anime;

    #endregion
}
