using System;
using System.Collections.Generic;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Filtering;
using Shoko.Server.Server;

namespace Shoko.Tests;

public class TestFilterable : IFilterableInfo
{
    public string Name { get; init; }
    public IReadOnlySet<string> Names { get; init; }
    public IReadOnlySet<string> AniDBIDs { get; init; }
    public string SortingName { get; init; }
    public int SeriesCount { get; init; }
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
    public bool HasTraktLink { get; init; }
    public bool HasTraktAutoLinkingDisabled { get; init; }
    public bool HasMissingTraktLink { get; init; }
    public bool IsFinished { get; init; }
    public DateTime? AirDate { get; init; }
    public DateTime? LastAirDate { get; init; }
    public DateTime AddedDate { get; init; }
    public DateTime LastAddedDate { get; init; }
    public int EpisodeCount { get; init; }
    public int TotalEpisodeCount { get; init; }
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
    public IReadOnlySet<string> CharacterIDs { get; init; }
    public IReadOnlyDictionary<CastRoleType, IReadOnlySet<string>> CharacterAppearances { get; init; }
    public IReadOnlySet<string> CreatorIDs { get; init; }
    public IReadOnlyDictionary<CrewRoleType, IReadOnlySet<string>> CreatorRoles { get; init; }
    public IReadOnlySet<string> ReleaseGroupNames { get; init; }
}
