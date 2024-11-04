using System;
using System.Collections.Generic;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Tests;

public class TestFilterable : IFilterable
{
    public string Name { get; init; }
    public IReadOnlySet<string> Names { get; init; }
    public IReadOnlySet<string> AniDBIDs { get; init; }
    public string SortingName { get; init; }
    public int SeriesCount { get; init; }
    public int MissingEpisodes { get; init; }
    public int MissingEpisodesCollecting { get; init; }
    public IReadOnlySet<string> Tags { get; init; }
    public IReadOnlySet<string> CustomTags { get; init; }
    public IReadOnlySet<int> Years { get; init; }
    public IReadOnlySet<(int year, AnimeSeason season)> Seasons { get; init; }
    public IReadOnlySet<ImageEntityType> AvailableImageTypes { get; }
    public IReadOnlySet<ImageEntityType> PreferredImageTypes { get; }
    public bool HasTmdbLink { get; init; }
    public bool HasMissingTmdbLink { get; init; }
    public int AutomaticTmdbEpisodeLinks { get; init; }
    public int UserVerifiedTmdbEpisodeLinks { get; init; }
    public bool HasTraktLink { get; init; }
    public bool HasMissingTraktLink { get; init; }
    public bool IsFinished { get; init; }
    public DateTime? AirDate { get; init; }
    public DateTime? LastAirDate { get; init; }
    public DateTime AddedDate { get; init; }
    public DateTime LastAddedDate { get; init; }
    public int EpisodeCount { get; init; }
    public int TotalEpisodeCount { get; init; }
    public decimal LowestAniDBRating { get; init; }
    public decimal AverageAniDBRating { get; init; }
    public decimal HighestAniDBRating { get; init; }
    public IReadOnlySet<string> VideoSources { get; init; }
    public IReadOnlySet<string> SharedVideoSources { get; init; }
    public IReadOnlySet<string> AnimeTypes { get; init; }
    public IReadOnlySet<string> AudioLanguages { get; init; }
    public IReadOnlySet<string> SharedAudioLanguages { get; init; }
    public IReadOnlySet<string> SubtitleLanguages { get; init; }
    public IReadOnlySet<string> SharedSubtitleLanguages { get; init; }
    public IReadOnlySet<string> Resolutions { get; init; }
    public IReadOnlySet<string> ImportFolderIDs { get; init; }
    public IReadOnlySet<string> ImportFolderNames { get; init; }
    public IReadOnlySet<string> FilePaths { get; init; }
}
