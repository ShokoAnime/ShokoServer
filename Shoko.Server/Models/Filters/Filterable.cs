using System;
using System.Collections.Generic;
using Shoko.Models.Enums;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters;

public class Filterable : IFilterable
{
    public int MissingEpisodes { get; init; }
    public int MissingEpisodesCollecting { get; init; }
    public IReadOnlySet<string> Tags { get; init; }
    public IReadOnlySet<string> CustomTags { get; init; }
    public IReadOnlySet<int> Years { get; init; }
    public IReadOnlySet<(int year, AnimeSeason season)> Seasons { get; init; }
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
    public IReadOnlySet<string> VideoSources { get; init; }
    public IReadOnlySet<string> AnimeTypes { get; init; }
    public IReadOnlySet<string> AudioLanguages { get; init; }
    public IReadOnlySet<string> SubtitleLanguages { get; init; }
}
