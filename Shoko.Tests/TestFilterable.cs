using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Tests;

public class TestFilterable : IFilterableInfo
{
    public string Name { get; init; } = null!;
    public string MainName { get; init; } = null!;
    public string OriginalName { get; init; } = null!;
    public string SortName { get; init; } = null!;
    public IReadOnlySet<string> Names { get; init; } = null!;
    public IReadOnlySet<string> PreferredNames { get; init; } = null!;
    public string Description { get; init; } = null!;
    public IReadOnlySet<string> Descriptions { get; init; } = null!;
    public IReadOnlySet<string> SeriesIDs { get; init; } = null!;
    public int GroupID { get; init; }
    public int TopLevelGroupID { get; init; }
    public IReadOnlySet<string> GroupIDs { get; init; } = null!;
    public IReadOnlySet<string> AnidbAnimeIDs { get; init; } = null!;
    public int SeriesCount { get; init; }
    public int GroupCount { get; init; }
    public int TotalGroupCount { get; init; }
    public int MissingEpisodes { get; init; }
    public int MissingEpisodesCollecting { get; init; }
    public int VideoFiles { get; init; }
    public IReadOnlySet<string> AnidbTagIDs { get; init; } = null!;
    public IReadOnlySet<string> AnidbTags { get; init; } = null!;
    public IReadOnlySet<string> CustomTagIDs { get; init; } = null!;
    public IReadOnlySet<string> CustomTags { get; init; } = null!;
    public IReadOnlySet<int> Years { get; init; } = null!;
    public IReadOnlySet<(int year, YearlySeason season)> Seasons { get; init; } = null!;
    public IReadOnlySet<ImageEntityType> AvailableImageTypes { get; } = null!;
    public IReadOnlySet<ImageEntityType> PreferredImageTypes { get; } = null!;
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
    public EpisodeCounts EpisodeCounts { get; init; } = null!;
    public EpisodeCounts LocalEpisodeCounts { get; init; } = null!;
    public EpisodeCounts MissingEpisodeCounts { get; init; } = null!;
    public EpisodeCounts UnairedEpisodeCounts { get; init; } = null!;
    public FileSourceCounts FileSourceCounts { get; init; } = null!;
    public IReadOnlyDictionary<string, int> ReleaseProviderCounts { get; init; } = null!;
    public double LowestAniDBRating { get; init; }
    public double AverageAniDBRating { get; init; }
    public double HighestAniDBRating { get; init; }
    public IReadOnlySet<string> VideoSources { get; init; } = null!;
    public IReadOnlySet<string> SharedVideoSources { get; init; } = null!;
    public IReadOnlySet<AnimeType> AnimeTypes { get; init; } = null!;
    public IReadOnlySet<string> AudioLanguages { get; init; } = null!;
    public IReadOnlySet<string> SharedAudioLanguages { get; init; } = null!;
    public IReadOnlySet<string> SubtitleLanguages { get; init; } = null!;
    public IReadOnlySet<string> SharedSubtitleLanguages { get; init; } = null!;
    public IReadOnlySet<string> Resolutions { get; init; } = null!;
    public IReadOnlySet<string> ManagedFolderIDs { get; init; } = null!;
    public IReadOnlySet<string> ManagedFolderNames { get; init; } = null!;
    public IReadOnlySet<string> FilePaths { get; init; } = null!;
    public IReadOnlySet<string> AbsoluteFilePaths { get; init; } = null!;
    public IReadOnlySet<string> ContainingFolderPaths { get; init; } = null!;
    public IReadOnlySet<string> CharacterIDs { get; init; } = null!;
    public IReadOnlyDictionary<CastRoleType, IReadOnlySet<string>> CharacterAppearances { get; init; } = null!;
    public IReadOnlySet<string> CreatorIDs { get; init; } = null!;
    public IReadOnlyDictionary<CrewRoleType, IReadOnlySet<string>> CreatorRoles { get; init; } = null!;
    public IReadOnlySet<string> ReleaseGroupNames { get; init; } = null!;
    public IReadOnlySet<string> ReleaseProviderNames { get; init; } = null!;
    public IReadOnlySet<string> TmdbMovieKeywords { get; init; } = null!;
    public IReadOnlySet<string> TmdbMovieGenres { get; init; } = null!;
    public IReadOnlySet<string> TmdbShowKeywords { get; init; } = null!;
    public IReadOnlySet<string> TmdbShowGenres { get; init; } = null!;
    public IReadOnlySet<string> TmdbKeywords { get; init; } = null!;
    public IReadOnlySet<string> TmdbGenres { get; init; } = null!;
}
