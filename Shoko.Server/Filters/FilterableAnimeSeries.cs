using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.MediaInfo;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.Filters;

public sealed class FilterableAnimeSeries(AnimeSeries series, DateTime now) : IFilterableInfo
{
    private static readonly HashSet<TitleLanguage> s_basePreferredLanguages =
    [
        TitleLanguage.Unknown, TitleLanguage.English, TitleLanguage.Japanese,
        TitleLanguage.Romaji, TitleLanguage.Korean, TitleLanguage.Chinese,
        TitleLanguage.ChineseSimplified, TitleLanguage.ChineseTraditional, TitleLanguage.Pinyin,
    ];

    private readonly AniDB_Anime? _anime = series.AniDB_Anime;

    private IReadOnlyList<VideoLocal>? _videoLocals;
    private IReadOnlyList<VideoLocal> VideoLocals => _videoLocals ??= series.VideoLocals;

    private IReadOnlyList<AniDB_Anime_Character>? _characterRefs;
    private IReadOnlyList<AniDB_Anime_Character> CharacterRefs =>
        _characterRefs ??= RepoFactory.AniDB_Anime_Character.GetByAnimeID(series.AniDB_ID);

    private IReadOnlyList<AniDB_Anime_Staff>? _staffRefs;
    private IReadOnlyList<AniDB_Anime_Staff> StaffRefs =>
        _staffRefs ??= RepoFactory.AniDB_Anime_Staff.GetByAnimeID(series.AniDB_ID);

    private IReadOnlyList<AniDB_Anime_Character_Creator>? _charCreatorRefs;
    private IReadOnlyList<AniDB_Anime_Character_Creator> CharacterCreatorRefs =>
        _charCreatorRefs ??= RepoFactory.AniDB_Anime_Character_Creator.GetByAnimeID(series.AniDB_ID);

    private static HashSet<TitleLanguage> BuildPreferredLanguageSet()
    {
        var result = new HashSet<TitleLanguage>(s_basePreferredLanguages);
        foreach (var langCode in ISettingsProvider.Instance.GetSettings().Language.SeriesTitleLanguageOrder)
            result.Add(langCode.GetTitleLanguage());
        return result;
    }

    public string Name => series.Title;

    public string SortName => series.Title.ToSortName();

    public string MainName => _anime?.MainTitle ?? string.Empty;

    public string OriginalName => _anime?.OriginalTitle ?? string.Empty;

    public IReadOnlySet<string> Names
    {
        get
        {
            var titles = series.Titles.Select(t => t.Value).ToHashSet();
            foreach (var group in series.AllGroupsAbove)
                titles.Add(group.GroupName);
            return titles;
        }
    }

    public IReadOnlySet<string> PreferredNames
    {
        get
        {
            var langs = BuildPreferredLanguageSet();
            var titles = series.Titles
                .Where(t => langs.Contains(t.Language) && (t.Source != DataSource.TMDB || t.Language != TitleLanguage.Unknown))
                .Select(t => t.Value)
                .ToHashSet();
            foreach (var group in series.AllGroupsAbove)
                titles.Add(group.GroupName);
            return titles;
        }
    }

    public string Description => series.PreferredOverview?.Value ?? string.Empty;

    public IReadOnlySet<string> Descriptions =>
        ((ISeries)series).Descriptions.Select(a => a.Value).ToHashSet();

    public IReadOnlySet<string> SeriesIDs => new HashSet<string> { series.AnimeSeriesID.ToString() };

    public int GroupID => series.AnimeGroupID;

    public int TopLevelGroupID => series.TopLevelAnimeGroup.AnimeGroupID;

    public IReadOnlySet<string> GroupIDs =>
        series.AllGroupsAbove.Select(a => a.AnimeGroupID.ToString()).ToHashSet();

    public IReadOnlySet<string> AnidbAnimeIDs => new HashSet<string> { series.AniDB_ID.ToString() };

    public int SeriesCount => 1;

    public int GroupCount => 0;

    public int TotalGroupCount => 0;

    public PartialDateOnly? AirDate => _anime?.AirDate;

    public PartialDateOnly? LastAirDate =>
        series.EndDate ?? series.AllAnimeEpisodes
            .Select(a => a.AniDB_Episode?.GetAirDateAsPartialDateOnly())
            .WhereNotNull()
            .Cast<PartialDateOnly?>()
            .Max();

    public DateTime AddedDate => series.DateTimeCreated;

    public DateTime? LastAddedDate =>
        VideoLocals.Select(a => a.DateTimeCreated).DefaultIfEmpty().Max();

    public int MissingEpisodes => series.MissingEpisodeCount;

    public int MissingEpisodesCollecting => series.MissingEpisodeCountGroups;

    public int VideoFiles => VideoLocals.Count;

    public IReadOnlySet<string> AnidbTagIDs =>
        _anime?.Tags.Select(a => a.TagID.ToString()).ToHashSet() ?? [];

    public IReadOnlySet<string> AnidbTags =>
        _anime?.Tags.Select(a => a.TagName).ToHashSet(StringComparer.InvariantCultureIgnoreCase) ?? [];

    public IReadOnlySet<string> CustomTagIDs =>
        _anime?.CustomTags.Select(a => a.CustomTagID.ToString()).ToHashSet() ?? [];

    public IReadOnlySet<string> CustomTags =>
        _anime?.CustomTags.Select(a => a.TagName).ToHashSet(StringComparer.InvariantCultureIgnoreCase) ?? [];

    public IReadOnlySet<int> Years => series.Years;

    public IReadOnlySet<(int year, YearlySeason season)> Seasons =>
        _anime?.YearlySeasons.ToHashSet() ?? [];

    public IReadOnlySet<ImageEntityType> AvailableImageTypes => series.AvailableImageTypes;

    public IReadOnlySet<ImageEntityType> PreferredImageTypes => series.PreferredImageTypes;

    public IReadOnlySet<string> CharacterIDs =>
        CharacterRefs.Select(a => a.CharacterID.ToString()).ToHashSet();

    public IReadOnlyDictionary<CastRoleType, IReadOnlySet<string>> CharacterAppearances =>
        CharacterRefs
            .GroupBy(a => a.CastRoleType)
            .ToDictionary(a => a.Key, a => (IReadOnlySet<string>)a.Select(b => b.CharacterID.ToString()).ToHashSet());

    public IReadOnlySet<string> CreatorIDs =>
        CharacterCreatorRefs.Select(a => a.CreatorID.ToString())
            .Concat(StaffRefs.Select(a => a.CreatorID.ToString()))
            .ToHashSet();

    public IReadOnlyDictionary<CrewRoleType, IReadOnlySet<string>> CreatorRoles =>
        StaffRefs.Select(a => (a.CrewRoleType, a.CreatorID))
            .Concat(CharacterCreatorRefs.Select(a => (CrewRoleType: CrewRoleType.Actor, a.CreatorID)))
            .GroupBy(a => a.CrewRoleType)
            .ToDictionary(a => a.Key, a => (IReadOnlySet<string>)a.Select(b => b.CreatorID.ToString()).ToHashSet());

    public bool HasTmdbLink =>
        series.TmdbShowCrossReferences.Count is > 0 || series.TmdbMovieCrossReferences.Count is > 0;

    public bool HasTmdbAutoLinkingDisabled => series.IsTmdbAutoMatchingDisabled;

    public int AutomaticTmdbEpisodeLinks =>
        series.TmdbEpisodeCrossReferences.Count(xref => xref.MatchRating is not MatchRating.UserVerified) +
        series.TmdbMovieCrossReferences.Count(xref => xref.MatchRating is not MatchRating.UserVerified);

    public int UserVerifiedTmdbEpisodeLinks =>
        series.TmdbEpisodeCrossReferences.Count(xref => xref.MatchRating is MatchRating.UserVerified) +
        series.TmdbMovieCrossReferences.Count(xref => xref.MatchRating is MatchRating.UserVerified);

    public int MissingTmdbEpisodeLinks
    {
        get
        {
            var allTmdbLinkedEpisodes = series.TmdbEpisodeCrossReferences.Select(a => a.AnidbEpisodeID)
                .Concat(series.TmdbMovieCrossReferences.Select(a => a.AnidbEpisodeID))
                .ToHashSet();
            return series.AnimeEpisodes.Count(a => !allTmdbLinkedEpisodes.Contains(a.AnimeEpisodeID));
        }
    }

    public IReadOnlySet<string> TmdbMovieKeywords =>
        series.TmdbMovies.SelectMany(m => m.Keywords)
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

    public IReadOnlySet<string> TmdbMovieGenres =>
        series.TmdbMovies.SelectMany(m => m.Genres)
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

    public IReadOnlySet<string> TmdbShowKeywords =>
        series.TmdbShows.SelectMany(s => s.Keywords)
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

    public IReadOnlySet<string> TmdbShowGenres =>
        series.TmdbShows.SelectMany(s => s.Genres)
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

    public IReadOnlySet<string> TmdbKeywords =>
        TmdbMovieKeywords.Union(TmdbShowKeywords)
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

    public IReadOnlySet<string> TmdbGenres =>
        TmdbMovieGenres.Union(TmdbShowGenres)
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

    public bool HasAnilistLink => false;

    public bool HasAnilistAutoLinkingDisabled => series.IsAnilistAutoMatchingDisabled;

    public int AutomaticAnilistEpisodeLinks => 0;

    public int UserVerifiedAnilistEpisodeLinks => 0;

    public int MissingAnilistEpisodeLinks => 0;

    public bool IsFinished => _anime?.EndDate is { } endDate && endDate < now.Date;

    public bool IsRestricted => _anime?.IsRestricted ?? false;

    public int EpisodeCount => _anime?.EpisodeCountNormal ?? 0;

    public int TotalEpisodeCount => _anime?.EpisodeCount ?? 0;

    public int HiddenEpisodes => series.AnimeEpisodes.Count(ep => ep.IsHidden);

    public EpisodeCounts EpisodeCounts => ((ISeries)series).EpisodeCounts;

    public EpisodeCounts LocalEpisodeCounts => ((IShokoSeries)series).LocalEpisodeCounts;

    public EpisodeCounts MissingEpisodeCounts => ((IShokoSeries)series).MissingEpisodeCounts;

    public EpisodeCounts UnairedEpisodeCounts => ((IShokoSeries)series).UnairedEpisodeCounts;

    public FileSourceCounts FileSourceCounts => ((IShokoSeries)series).FileSourceCounts;

    public IReadOnlyDictionary<string, int> ReleaseProviderCounts => ((IShokoSeries)series).ReleaseProviderCounts;

    public double LowestAniDBRating =>
        double.Round(Convert.ToDouble(_anime?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero);

    public double HighestAniDBRating =>
        double.Round(Convert.ToDouble(_anime?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero);

    public double AverageAniDBRating =>
        double.Round(Convert.ToDouble(_anime?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero);

    public IReadOnlySet<AnimeType> AnimeTypes =>
        _anime is { } anime ? new HashSet<AnimeType> { anime.AnimeType } : [];

    public IReadOnlySet<string> VideoSources =>
        VideoLocals.Select(a => a.ReleaseInfo).WhereNotNull().Select(a => a.LegacySource).ToHashSet();

    public IReadOnlySet<string> SharedVideoSources =>
        VideoLocals.Select(b => b.ReleaseInfo).WhereNotNull().Select(a => a.LegacySource).ToHashSet() is { Count: > 0 } sources
            ? sources
            : [];

    public IReadOnlySet<string> AudioLanguages =>
        VideoLocals.Select(a => a.ReleaseInfo).WhereNotNull()
            .SelectMany(a => a.AudioLanguages?.Select(b => b.ToString()) ?? [])
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

    public IReadOnlySet<string> SharedAudioLanguages =>
        VideoLocals.Select(b => b.ReleaseInfo).WhereNotNull()
            .Select(a => a.AudioLanguages?.Select(b => b.GetString()) ?? [])
            .ToList() is { Count: > 0 } audioNames
            ? audioNames.Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet()
            : [];

    public IReadOnlySet<string> SubtitleLanguages =>
        VideoLocals.Select(a => a.ReleaseInfo).WhereNotNull()
            .SelectMany(a => a.SubtitleLanguages?.Select(b => b.ToString()) ?? [])
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

    public IReadOnlySet<string> SharedSubtitleLanguages =>
        VideoLocals.Select(b => b.ReleaseInfo).WhereNotNull()
            .Select(a => a.SubtitleLanguages?.Select(b => b.GetString()) ?? [])
            .ToList() is { Count: > 0 } subtitleNames
            ? subtitleNames.Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet()
            : [];

    public IReadOnlySet<string> Resolutions =>
        VideoLocals
            .Where(a => a.MediaInfo?.VideoStream is not null)
            .Select(a => MediaInfoUtility.GetStandardResolution(Tuple.Create(a.MediaInfo!.VideoStream!.Width, a.MediaInfo!.VideoStream!.Height)))
            .WhereNotNull()
            .ToHashSet();

    public IReadOnlySet<string> ManagedFolderIDs =>
        VideoLocals.Select(a => a.FirstValidPlace?.ManagedFolderID.ToString()).WhereNotNull().ToHashSet();

    public IReadOnlySet<string> ManagedFolderNames =>
        VideoLocals.Select(a => a.FirstValidPlace?.ManagedFolder?.Name).WhereNotNull().ToHashSet();

    public IReadOnlySet<string> FilePaths =>
        VideoLocals.SelectMany(a => a.Places.Select(b => b.RelativePath)).ToHashSet();

    public IReadOnlySet<string> AbsoluteFilePaths =>
        VideoLocals.SelectMany(a => a.Places)
            .Select(b => Path.Join(b.ManagedFolder!.Path, b.RelativePath))
            .ToHashSet();

    public IReadOnlySet<string> ContainingFolderPaths =>
        VideoLocals.SelectMany(a => a.Places)
            .Select(b => Path.GetDirectoryName(Path.Join(b.ManagedFolder!.Path, b.RelativePath))!)
            .ToHashSet();

    public IReadOnlySet<string> ReleaseGroupNames =>
        VideoLocals.Select(a => a.ReleaseGroup?.Name).WhereNotNull().ToHashSet();

    public IReadOnlySet<string> ReleaseProviderNames =>
        VideoLocals.SelectMany(a => a.ReleaseInfo?.ProviderName.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [])
            .ToHashSet();
}
