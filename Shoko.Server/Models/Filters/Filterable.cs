using System;
using System.Collections.Generic;
using Shoko.Models.Enums;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters;

public class Filterable : IFilterable
{
    // The explicit implementations of IReadOnlySet make it easier to deserialize into
    public string Name { get; init; }
    public string SortingName { get; init; }
    public int SeriesCount { get; init; }
    public int MissingEpisodes { get; init; }
    public int MissingEpisodesCollecting { get; init; }
    public HashSet<string> Tags { get; init; }
    IReadOnlySet<string> IFilterable.Tags => Tags;
    public HashSet<string> CustomTags { get; init; }
    IReadOnlySet<string> IFilterable.CustomTags => CustomTags;
    public HashSet<int> Years { get; init; }
    IReadOnlySet<int> IFilterable.Years => Years;
    public HashSet<(int year, AnimeSeason season)> Seasons { get; init; }
    IReadOnlySet<(int year, AnimeSeason season)> IFilterable.Seasons => Seasons;
    public bool HasTvDBLink { get; init; }
    public bool HasMissingTvDbLink { get; init; }
    public bool HasTMDbLink { get; init; }
    public bool HasMissingTMDbLink { get; init; }
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
    public decimal HighestAniDBRating { get; init; }
    public HashSet<string> VideoSources { get; init; }
    IReadOnlySet<string> IFilterable.VideoSources => VideoSources;
    public HashSet<string> AnimeTypes { get; init; }
    IReadOnlySet<string> IFilterable.AnimeTypes => AnimeTypes;
    public HashSet<string> AudioLanguages { get; init; }
    IReadOnlySet<string> IFilterable.AudioLanguages => AudioLanguages;
    public HashSet<string> SubtitleLanguages { get; init; }
    IReadOnlySet<string> IFilterable.SubtitleLanguages => SubtitleLanguages;
}
