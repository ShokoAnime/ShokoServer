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

public sealed class FilterableAnimeGroup(AnimeGroup group, DateTime now) : IFilterableInfo
{
    private static readonly HashSet<TitleLanguage> s_basePreferredLanguages =
    [
        TitleLanguage.Unknown, TitleLanguage.English, TitleLanguage.Japanese,
        TitleLanguage.Romaji, TitleLanguage.Korean, TitleLanguage.Chinese,
        TitleLanguage.ChineseSimplified, TitleLanguage.ChineseTraditional, TitleLanguage.Pinyin,
    ];

    private List<AnimeSeries>? _series;
    private List<AnimeSeries> AllSeries => _series ??= group.AllSeries;

    // Derived from AllSeries to avoid a redundant repo call that group.Anime would make
    private List<AniDB_Anime>? _anime;
    private List<AniDB_Anime> AllAnime => _anime ??= AllSeries.Select(s => s.AniDB_Anime).WhereNotNull().ToList();

    private IReadOnlyList<VideoLocal>? _allVideoLocals;
    private IReadOnlyList<VideoLocal> AllVideoLocals => _allVideoLocals ??=
        AllSeries.SelectMany(s => s.VideoLocals).DistinctBy(a => a.VideoLocalID).ToList();

    private static HashSet<TitleLanguage> BuildPreferredLanguageSet()
    {
        var result = new HashSet<TitleLanguage>(s_basePreferredLanguages);
        foreach (var langCode in ISettingsProvider.Instance.GetSettings().Language.SeriesTitleLanguageOrder)
            result.Add(langCode.GetTitleLanguage());
        return result;
    }

    public string Name => group.GroupName;

    public string SortName => group.GroupName.ToSortName();

    public string MainName => group.GroupName;

    public string OriginalName => group.GroupName;

    public IReadOnlySet<string> Names
    {
        get
        {
            var result = new HashSet<string> { group.GroupName };
            foreach (var grp in group.AllGroupsAbove)
                result.Add(grp.GroupName);
            result.UnionWith(AllSeries.SelectMany(a => a.Titles.Select(t => t.Value)));
            return result;
        }
    }

    public IReadOnlySet<string> PreferredNames
    {
        get
        {
            var langs = BuildPreferredLanguageSet();
            var result = new HashSet<string> { group.GroupName };
            foreach (var grp in group.AllGroupsAbove)
                result.Add(grp.GroupName);
            result.UnionWith(AllSeries.SelectMany(a =>
                a.Titles
                    .Where(t => langs.Contains(t.Language) && (t.Source != DataSource.TMDB || t.Language != TitleLanguage.Unknown))
                    .Select(t => t.Value)));
            return result;
        }
    }

    public string Description => group.Description;

    public IReadOnlySet<string> Descriptions
    {
        get
        {
            var result = new HashSet<string> { group.Description };
            result.UnionWith(AllSeries.SelectMany(s => ((ISeries)s).Descriptions.Select(a => a.Value)));
            return result;
        }
    }

    public IReadOnlySet<string> SeriesIDs =>
        AllSeries.Select(a => a.AnimeSeriesID.ToString()).ToHashSet();

    public int GroupID => group.AnimeGroupID;

    public int TopLevelGroupID => group.TopLevelAnimeGroup.AnimeGroupID;

    public IReadOnlySet<string> GroupIDs =>
        group.AllGroupsAbove.Prepend(group).Select(a => a.AnimeGroupID.ToString()).ToHashSet();

    public IReadOnlySet<string> AnidbAnimeIDs =>
        AllSeries.Select(a => a.AniDB_ID.ToString()).ToHashSet();

    public int SeriesCount => AllSeries.Count;

    public int GroupCount => group.Children.Count;

    public int TotalGroupCount => group.AllChildren.Count();

    public PartialDateOnly? AirDate =>
        AllSeries.Select(a => a.AirDate).WhereNotNull().DefaultIfEmpty(PartialDateOnly.MaxValue).Min();

    public PartialDateOnly? LastAirDate =>
        AllSeries.SelectMany(a => a.AllAnimeEpisodes)
            .Select(a => a.AniDB_Episode?.GetAirDateAsPartialDateOnly())
            .WhereNotNull()
            .Cast<PartialDateOnly?>()
            .Max();

    public DateTime AddedDate => group.DateTimeCreated;

    public DateTime? LastAddedDate =>
        AllVideoLocals.Select(a => a.DateTimeCreated).DefaultIfEmpty().Max();

    public int MissingEpisodes => group.MissingEpisodeCount;

    public int MissingEpisodesCollecting => group.MissingEpisodeCountGroups;

    public int VideoFiles => AllVideoLocals.Count;

    public IReadOnlySet<string> AnidbTagIDs =>
        group.Tags.Select(a => a.TagID.ToString()).ToHashSet();

    public IReadOnlySet<string> AnidbTags =>
        group.Tags.Select(a => a.TagName).ToHashSet(StringComparer.InvariantCultureIgnoreCase);

    public IReadOnlySet<string> CustomTagIDs =>
        group.CustomTags.Select(a => a.CustomTagID.ToString()).ToHashSet();

    public IReadOnlySet<string> CustomTags =>
        group.CustomTags.Select(a => a.TagName).ToHashSet(StringComparer.InvariantCultureIgnoreCase);

    public IReadOnlySet<int> Years => group.Years;

    public IReadOnlySet<(int year, YearlySeason season)> Seasons => group.YearlySeasons;

    public IReadOnlySet<ImageEntityType> AvailableImageTypes => group.AvailableImageTypes;

    public IReadOnlySet<ImageEntityType> PreferredImageTypes => group.PreferredImageTypes;

    public IReadOnlySet<string> CharacterIDs =>
        AllSeries.SelectMany(ser => RepoFactory.AniDB_Anime_Character.GetByAnimeID(ser.AniDB_ID))
            .Select(a => a.CharacterID.ToString())
            .ToHashSet();

    public IReadOnlyDictionary<CastRoleType, IReadOnlySet<string>> CharacterAppearances =>
        AllSeries.SelectMany(ser => RepoFactory.AniDB_Anime_Character.GetByAnimeID(ser.AniDB_ID))
            .DistinctBy(a => (a.CastRoleType, a.CharacterID))
            .GroupBy(a => a.CastRoleType)
            .ToDictionary(a => a.Key, a => (IReadOnlySet<string>)a.Select(b => b.CharacterID.ToString()).ToHashSet());

    public IReadOnlySet<string> CreatorIDs =>
        AllSeries.SelectMany(ser => RepoFactory.AniDB_Anime_Character_Creator.GetByAnimeID(ser.AniDB_ID))
            .Select(a => a.CreatorID.ToString())
            .Concat(AllSeries.SelectMany(ser => RepoFactory.AniDB_Anime_Staff.GetByAnimeID(ser.AniDB_ID).Select(a => a.CreatorID.ToString())))
            .ToHashSet();

    public IReadOnlyDictionary<CrewRoleType, IReadOnlySet<string>> CreatorRoles =>
        AllSeries.SelectMany(ser => RepoFactory.AniDB_Anime_Staff.GetByAnimeID(ser.AniDB_ID))
            .Select(a => (a.CrewRoleType, a.CreatorID))
            .DistinctBy(a => (a.CrewRoleType, a.CreatorID))
            .Concat(
                AllSeries.SelectMany(ser => RepoFactory.AniDB_Anime_Character_Creator.GetByAnimeID(ser.AniDB_ID)
                    .DistinctBy(a => a.CreatorID)
                    .Select(a => (CrewRoleType: CrewRoleType.Actor, a.CreatorID)))
            )
            .GroupBy(a => a.CrewRoleType)
            .ToDictionary(a => a.Key, a => (IReadOnlySet<string>)a.Select(b => b.CreatorID.ToString()).ToHashSet());

    public bool HasTmdbLink =>
        AllSeries.Any(a => a.TmdbShowCrossReferences.Count is > 0 || a.TmdbMovieCrossReferences.Count is > 0);

    public bool HasTmdbAutoLinkingDisabled =>
        AllSeries.Any(a => a.IsTmdbAutoMatchingDisabled);

    public int AutomaticTmdbEpisodeLinks =>
        AllSeries.Sum(a =>
            a.TmdbEpisodeCrossReferences.Count(xref => xref.MatchRating is not MatchRating.UserVerified) +
            a.TmdbMovieCrossReferences.Count(xref => xref.MatchRating is not MatchRating.UserVerified));

    public int UserVerifiedTmdbEpisodeLinks =>
        AllSeries.Sum(a =>
            a.TmdbEpisodeCrossReferences.Count(xref => xref.MatchRating is MatchRating.UserVerified) +
            a.TmdbMovieCrossReferences.Count(xref => xref.MatchRating is MatchRating.UserVerified));

    public int MissingTmdbEpisodeLinks => AllSeries.Aggregate(0, (acc, ser) =>
    {
        var allTmdbLinkedEpisodes = ser.TmdbEpisodeCrossReferences.Select(a => a.AnidbEpisodeID)
            .Concat(ser.TmdbMovieCrossReferences.Select(a => a.AnidbEpisodeID))
            .ToHashSet();
        return acc + ser.AnimeEpisodes.Count(a => !allTmdbLinkedEpisodes.Contains(a.AnimeEpisodeID));
    });

    public IReadOnlySet<string> TmdbMovieKeywords =>
        AllSeries.SelectMany(s => s.TmdbMovies)
            .DistinctBy(m => m.TmdbMovieID)
            .SelectMany(m => m.Keywords)
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

    public IReadOnlySet<string> TmdbMovieGenres =>
        AllSeries.SelectMany(s => s.TmdbMovies)
            .DistinctBy(m => m.TmdbMovieID)
            .SelectMany(m => m.Genres)
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

    public IReadOnlySet<string> TmdbShowKeywords =>
        AllSeries.SelectMany(s => s.TmdbShows)
            .DistinctBy(s => s.TmdbShowID)
            .SelectMany(s => s.Keywords)
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

    public IReadOnlySet<string> TmdbShowGenres =>
        AllSeries.SelectMany(s => s.TmdbShows)
            .DistinctBy(s => s.TmdbShowID)
            .SelectMany(s => s.Genres)
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

    public IReadOnlySet<string> TmdbKeywords =>
        TmdbMovieKeywords.Union(TmdbShowKeywords)
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

    public IReadOnlySet<string> TmdbGenres =>
        TmdbMovieGenres.Union(TmdbShowGenres)
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

    public bool HasAnilistLink => false;

    public bool HasAnilistAutoLinkingDisabled =>
        AllSeries.Any(a => a.IsAnilistAutoMatchingDisabled);

    public int AutomaticAnilistEpisodeLinks => 0;

    public int UserVerifiedAnilistEpisodeLinks => 0;

    public int MissingAnilistEpisodeLinks => 0;

    public bool IsFinished =>
        AllSeries.All(a => a.EndDate is not null && a.EndDate <= now.Date);

    public bool IsRestricted =>
        AllSeries.Any(a => a.AniDB_Anime?.IsRestricted ?? false);

    public int EpisodeCount =>
        AllSeries.Sum(a => a.AniDB_Anime?.EpisodeCountNormal ?? 0);

    public int TotalEpisodeCount =>
        AllSeries.Sum(a => a.AniDB_Anime?.EpisodeCount ?? 0);

    public int HiddenEpisodes =>
        AllSeries.SelectMany(ser => ser.AnimeEpisodes).Count(ep => ep.IsHidden);

    public EpisodeCounts EpisodeCounts => ((IShokoGroup)group).EpisodeCounts;

    public EpisodeCounts LocalEpisodeCounts => ((IShokoGroup)group).LocalEpisodeCounts;

    public EpisodeCounts MissingEpisodeCounts => ((IShokoGroup)group).MissingEpisodeCounts;

    public EpisodeCounts UnairedEpisodeCounts => ((IShokoGroup)group).UnairedEpisodeCounts;

    public FileSourceCounts FileSourceCounts => ((IShokoGroup)group).FileSourceCounts;

    public IReadOnlyDictionary<string, int> ReleaseProviderCounts => ((IShokoGroup)group).ReleaseProviderCounts;

    public double LowestAniDBRating =>
        AllAnime.Select(a => double.Round(Convert.ToDouble(a?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero)).DefaultIfEmpty().Min();

    public double HighestAniDBRating =>
        AllAnime.Select(a => double.Round(Convert.ToDouble(a?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero)).DefaultIfEmpty().Max();

    public double AverageAniDBRating =>
        AllAnime.Select(a => double.Round(Convert.ToDouble(a?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero)).DefaultIfEmpty().Average();

    public IReadOnlySet<AnimeType> AnimeTypes =>
        new HashSet<AnimeType>(AllAnime.Select(a => a.AnimeType));

    public IReadOnlySet<string> VideoSources =>
        AllVideoLocals.Select(a => a.ReleaseInfo).WhereNotNull().Select(a => a.LegacySource).ToHashSet();

    public IReadOnlySet<string> SharedVideoSources =>
        AllVideoLocals.Select(b => b.ReleaseInfo).WhereNotNull().Select(a => a.LegacySource).ToHashSet() is { Count: > 0 } sources
            ? sources
            : [];

    public IReadOnlySet<string> AudioLanguages =>
        AllVideoLocals.Select(a => a.ReleaseInfo).WhereNotNull()
            .SelectMany(a => a.AudioLanguages?.Select(b => b.GetString()) ?? [])
            .ToHashSet();

    public IReadOnlySet<string> SharedAudioLanguages =>
        AllVideoLocals.Select(b => b.ReleaseInfo).WhereNotNull()
            .Select(a => a.AudioLanguages?.Select(b => b.GetString()) ?? [])
            .ToList() is { Count: > 0 } audioLanguageNames
            ? audioLanguageNames.Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet()
            : [];

    public IReadOnlySet<string> SubtitleLanguages =>
        AllVideoLocals.Select(a => a.ReleaseInfo).WhereNotNull()
            .SelectMany(a => a.SubtitleLanguages?.Select(b => b.GetString()) ?? [])
            .ToHashSet();

    public IReadOnlySet<string> SharedSubtitleLanguages =>
        AllVideoLocals.Select(b => b.ReleaseInfo).WhereNotNull()
            .Select(a => a.SubtitleLanguages?.Select(b => b.GetString()) ?? [])
            .ToList() is { Count: > 0 } subtitleLanguageNames
            ? subtitleLanguageNames.Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet()
            : [];

    public IReadOnlySet<string> Resolutions =>
        AllVideoLocals
            .Where(a => a.MediaInfo?.VideoStream is not null)
            .Select(a => MediaInfoUtility.GetStandardResolution(Tuple.Create(a.MediaInfo!.VideoStream!.Width, a.MediaInfo!.VideoStream!.Height)))
            .WhereNotNull()
            .ToHashSet();

    public IReadOnlySet<string> ManagedFolderIDs =>
        AllSeries.SelectMany(s => s.VideoLocals.Select(a => a.FirstValidPlace?.ManagedFolderID.ToString()))
            .WhereNotNull()
            .ToHashSet();

    public IReadOnlySet<string> ManagedFolderNames =>
        AllSeries.SelectMany(s => s.VideoLocals.Select(a => a.FirstValidPlace?.ManagedFolder?.Name))
            .WhereNotNull()
            .ToHashSet();

    public IReadOnlySet<string> FilePaths =>
        AllVideoLocals.SelectMany(a => a.Places.Select(b => b.RelativePath)).ToHashSet();

    public IReadOnlySet<string> AbsoluteFilePaths =>
        AllVideoLocals.SelectMany(a => a.Places)
            .Select(b => Path.Join(b.ManagedFolder!.Path, b.RelativePath))
            .ToHashSet();

    public IReadOnlySet<string> ContainingFolderPaths =>
        AllVideoLocals.SelectMany(a => a.Places)
            .Select(b => Path.GetDirectoryName(Path.Join(b.ManagedFolder!.Path, b.RelativePath))!)
            .ToHashSet();

    public IReadOnlySet<string> ReleaseGroupNames =>
        AllVideoLocals.Select(a => a.ReleaseGroup?.Name).WhereNotNull().ToHashSet();

    public IReadOnlySet<string> ReleaseProviderNames =>
        AllVideoLocals.SelectMany(a => a.ReleaseInfo?.ProviderName.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [])
            .ToHashSet();
}
