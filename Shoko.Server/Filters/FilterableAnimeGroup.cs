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
using Shoko.Server.Models;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Services;
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
        => Remembered(TextMemoSlot.FilterNames, () =>
        {
            var result = new HashSet<string> { group.GroupName };
            foreach (var grp in group.AllGroupsAbove)
                result.Add(grp.GroupName);
            result.UnionWith(AllSeries.SelectMany(a => a.Titles.Select(t => t.Value)));
            return result;
        });

    public IReadOnlySet<string> PreferredNames
        => Remembered(TextMemoSlot.FilterPreferredNames, () =>
        {
            var langs = BuildPreferredLanguageSet();
            var result = new HashSet<string> { group.GroupName };
            foreach (var grp in group.AllGroupsAbove)
                result.Add(grp.GroupName);
            result.UnionWith(AllSeries.SelectMany(a =>
                a.Titles
                    .Where(t => langs.Contains(t.Language) && (t.Source != MetadataSource.TMDB || t.Language != TitleLanguage.Unknown))
                    .Select(t => t.Value)));
            return result;
        });

    /// <summary>
    ///   A set of names worked out once per group, so the same set is handed
    ///   to every filter until the texts or the series of the group change.
    /// </summary>
    /// <param name="slot">Which set.</param>
    /// <param name="build">Works the set out.</param>
    /// <returns>The set.</returns>
    private IReadOnlySet<string> Remembered(TextMemoSlot slot, Func<HashSet<string>> build)
    {
        if (TextAccess.Current is not { } manager)
            return build();

        var id = ((IMetadata)group).ID;
        if (manager.TryRemembered<IReadOnlySet<string>>(id, slot, out var names))
            return names;

        return manager.Remember<IReadOnlySet<string>>(id, slot, () =>
        {
            manager.RecordShapeOf(group);
            return build();
        }, () => build());
    }

    public string Description => group.Description;

    public IReadOnlySet<string> Descriptions
    {
        get
        {
            var result = new HashSet<string> { group.Description };
            result.UnionWith(AllSeries.SelectMany(s => ((ISeries)s).Overviews.Select(a => a.Value)));
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

    private List<AniDB_Tag>? _anidbTagRefs;
    private List<AniDB_Tag> AnidbTagRefs => _anidbTagRefs ??= group.Tags;

    private List<CustomTag>? _customTagRefs;
    private List<CustomTag> CustomTagRefs => _customTagRefs ??= group.CustomTags;

    private IReadOnlySet<string>? _anidbTagIDs;
    public IReadOnlySet<string> AnidbTagIDs =>
        _anidbTagIDs ??= AnidbTagRefs.Select(a => a.TagID.ToString()).ToHashSet();

    private IReadOnlySet<string>? _anidbTags;
    public IReadOnlySet<string> AnidbTags =>
        _anidbTags ??= AnidbTagRefs.Select(a => a.TagName).ToHashSet(StringComparer.InvariantCultureIgnoreCase);

    private IReadOnlySet<string>? _customTagIDs;
    public IReadOnlySet<string> CustomTagIDs =>
        _customTagIDs ??= CustomTagRefs.Select(a => a.CustomTagID.ToString()).ToHashSet();

    private IReadOnlySet<string>? _customTags;
    public IReadOnlySet<string> CustomTags =>
        _customTags ??= CustomTagRefs.Select(a => a.TagName).ToHashSet(StringComparer.InvariantCultureIgnoreCase);

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

    public IReadOnlySet<MetadataSource> LinkedSources => AllSeries.SelectMany(FilterableSources.LinkedSources).ToHashSet();

    public IReadOnlySet<MetadataSource> UnlinkedSources => AllSeries.SelectMany(FilterableSources.UnlinkedSources).ToHashSet();

    public IReadOnlySet<MetadataSource> AutoLinkingDisabledSources => AllSeries.SelectMany(FilterableSources.AutoLinkingDisabledSources).ToHashSet();

    public int GetAutomaticEpisodeLinks(MetadataSource source) => AllSeries.Sum(series => FilterableSources.EpisodeLinks(series, source, userVerified: false));

    public int GetUserVerifiedEpisodeLinks(MetadataSource source) => AllSeries.Sum(series => FilterableSources.EpisodeLinks(series, source, userVerified: true));

    public int GetMissingEpisodeLinks(MetadataSource source) => AllSeries.Sum(series => FilterableSources.MissingEpisodeLinks(series, source));

    public int GetAutomaticLinks(MetadataSource source) => AllSeries.Sum(series => FilterableSources.Links(series, source, userVerified: false));

    public int GetUserVerifiedLinks(MetadataSource source) => AllSeries.Sum(series => FilterableSources.Links(series, source, userVerified: true));

    public IReadOnlySet<string> GetGenres(MetadataSource source, MetadataEntityType? entityType = null)
        => AllSeries.SelectMany(series => FilterableSources.Genres(series, source, entityType)).ToNameSet();

    public IReadOnlySet<string> GetTags(MetadataSource source, MetadataEntityType? entityType = null)
        => AllSeries.SelectMany(series => FilterableSources.Tags(series, source, entityType)).ToNameSet();

    public int GetSuggestions(MetadataSource source) => source switch
    {
        _ when source == MetadataSource.AniDB => AnidbSuggestions,
        _ when source == MetadataSource.TMDB => TmdbSuggestions,
        _ => FilterableSuggestions.CountOther(SuggestionSources, source),
    };

    // Deduplicated across the series, since two series in one group can be linked to the same
    // provider entry and its suggestions would otherwise be counted twice.
    private SuggestionSources? _suggestionSources;
    private SuggestionSources SuggestionSources => _suggestionSources ??= new(
        [.. AllSeries.Select(a => a.AniDB_ID).Distinct()],
        [.. AllSeries.SelectMany(a => a.TmdbShowCrossReferences).Select(xref => xref.TmdbShowID).Distinct()],
        [.. AllSeries.SelectMany(a => a.TmdbMovieCrossReferences).Select(xref => xref.TmdbMovieID).Distinct()],
        [.. AllSeries.SelectMany(FilterableSources.OtherLinkedSeries).DistinctBy(entry => entry.ID)]
    );

    private int? _anidbSuggestions;
    public int AnidbSuggestions => _anidbSuggestions ??= FilterableSuggestions.CountAnidb(SuggestionSources);

    private int? _tmdbSuggestions;
    public int TmdbSuggestions => _tmdbSuggestions ??= FilterableSuggestions.CountTmdb(SuggestionSources);

    private int? _otherSuggestions;
    public int TotalSuggestions => AnidbSuggestions + TmdbSuggestions + (_otherSuggestions ??= FilterableSuggestions.CountOthers(SuggestionSources));

    private int? _localSuggestions;
    public int LocalSuggestions => _localSuggestions ??= FilterableSuggestions.CountLocal(SuggestionSources);

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
