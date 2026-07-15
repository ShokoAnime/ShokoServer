using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Tests;

public class TestFilterable : IFilterableInfo
{
    public string Name { get; init; }
    public string MainName { get; init; }
    public string OriginalName { get; init; }
    public string SortName { get; init; }
    public IReadOnlySet<string> Names { get; init; }
    public IReadOnlySet<string> PreferredNames { get; init; }
    public string Description { get; init; }
    public IReadOnlySet<string> Descriptions { get; init; }
    public IReadOnlySet<string> SeriesIDs { get; init; }
    public int GroupID { get; init; }
    public int TopLevelGroupID { get; init; }
    public IReadOnlySet<string> GroupIDs { get; init; }
    public IReadOnlySet<string> AnidbAnimeIDs { get; init; }
    public int SeriesCount { get; init; }
    public int GroupCount { get; init; }
    public int TotalGroupCount { get; init; }
    public int MissingEpisodes { get; init; }
    public int MissingEpisodesCollecting { get; init; }
    public int VideoFiles { get; init; }
    public IReadOnlySet<string> AnidbTagIDs { get; init; }
    public IReadOnlySet<string> AnidbTags { get; init; }
    public IReadOnlySet<string> CustomTagIDs { get; init; }
    public IReadOnlySet<string> CustomTags { get; init; }
    public IReadOnlySet<int> Years { get; init; }
    public IReadOnlySet<(int year, YearlySeason season)> Seasons { get; init; }
    public IReadOnlySet<ImageEntityType> AvailableImageTypes { get; }
    public IReadOnlySet<ImageEntityType> PreferredImageTypes { get; }
    public bool HasTmdbLink { get; init; }
    public bool HasTmdbAutoLinkingDisabled { get; init; }
    public bool HasMissingTmdbLink { get; init; }
    public int MissingTmdbEpisodeLinks { get; init; }
    public int AutomaticTmdbEpisodeLinks { get; init; }
    public int UserVerifiedTmdbEpisodeLinks { get; init; }
    public bool HasAnilistLink { get; init; }
    public bool HasAnilistAutoLinkingDisabled { get; init; }
    public bool HasMissingAnilistLink { get; init; }
    public int MissingAnilistEpisodeLinks { get; init; }
    public int AutomaticAnilistEpisodeLinks { get; init; }
    public int UserVerifiedAnilistEpisodeLinks { get; init; }
    public bool HasTraktLink { get; init; }
    public bool HasTraktAutoLinkingDisabled { get; init; }
    public bool HasMissingTraktLink { get; init; }
    public bool IsFinished { get; init; }
    public bool IsRestricted { get; init; }
    public PartialDateOnly? AirDate { get; init; }
    public PartialDateOnly? LastAirDate { get; init; }
    public DateTime AddedDate { get; init; }
    public DateTime? LastAddedDate { get; init; }
    public int EpisodeCount { get; init; }
    public int TotalEpisodeCount { get; init; }
    public int HiddenEpisodes { get; init; }
    public EpisodeCounts EpisodeCounts { get; init; }
    public EpisodeCounts LocalEpisodeCounts { get; init; }
    public EpisodeCounts MissingEpisodeCounts { get; init; }
    public EpisodeCounts UnairedEpisodeCounts { get; init; }
    public FileSourceCounts FileSourceCounts { get; init; }
    public IReadOnlyDictionary<string, int> ReleaseProviderCounts { get; init; }
    public double LowestAniDBRating { get; init; }
    public double AverageAniDBRating { get; init; }
    public double HighestAniDBRating { get; init; }
    public IReadOnlySet<string> VideoSources { get; init; }
    public IReadOnlySet<string> SharedVideoSources { get; init; }
    public IReadOnlySet<AnimeType> AnimeTypes { get; init; }
    public IReadOnlySet<string> AudioLanguages { get; init; }
    public IReadOnlySet<string> SharedAudioLanguages { get; init; }
    public IReadOnlySet<string> SubtitleLanguages { get; init; }
    public IReadOnlySet<string> SharedSubtitleLanguages { get; init; }
    public IReadOnlySet<string> Resolutions { get; init; }
    public IReadOnlySet<string> ManagedFolderIDs { get; init; }
    public IReadOnlySet<string> ManagedFolderNames { get; init; }
    public IReadOnlySet<string> FilePaths { get; init; }
    public IReadOnlySet<string> AbsoluteFilePaths { get; init; }
    public IReadOnlySet<string> ContainingFolderPaths { get; init; }
    public IReadOnlySet<string> CharacterIDs { get; init; }
    public IReadOnlyDictionary<CastRoleType, IReadOnlySet<string>> CharacterAppearances { get; init; }
    public IReadOnlySet<string> CreatorIDs { get; init; }
    public IReadOnlyDictionary<CrewRoleType, IReadOnlySet<string>> CreatorRoles { get; init; }
    public IReadOnlySet<string> ReleaseGroupNames { get; init; }
    public IReadOnlySet<string> ReleaseProviderNames { get; init; }
    public IReadOnlySet<string> TmdbMovieKeywords { get; init; }
    public IReadOnlySet<string> TmdbMovieGenres { get; init; }
    public IReadOnlySet<string> TmdbShowKeywords { get; init; }
    public IReadOnlySet<string> TmdbShowGenres { get; init; }
    public IReadOnlySet<string> TmdbKeywords { get; init; }
    public IReadOnlySet<string> TmdbGenres { get; init; }
}
