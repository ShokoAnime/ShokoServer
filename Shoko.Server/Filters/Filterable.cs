using System;
using System.Collections.Generic;
using Shoko.Models.Enums;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters;

public class Filterable : IFilterable
{
    private readonly Lazy<DateTime> _addedDate;
    private readonly Lazy<DateTime?> _airDate;
    private readonly Lazy<IReadOnlySet<string>> _animeTypes;
    private readonly Lazy<IReadOnlySet<string>> _audioLanguages;
    private readonly Lazy<decimal> _averageAniDBRating;
    private readonly Lazy<IReadOnlySet<string>> _customTags;
    private readonly Lazy<int> _episodeCount;
    private readonly Lazy<bool> _hasMissingTMDbLink;
    private readonly Lazy<bool> _hasMissingTraktLink;
    private readonly Lazy<bool> _hasMissingTvDBLink;
    private readonly Lazy<bool> _hasTMDbLink;
    private readonly Lazy<bool> _hasTraktLink;
    private readonly Lazy<bool> _hasTvDBLink;
    private readonly Lazy<decimal> _highestAniDBRating;
    private readonly Lazy<bool> _isFinished;
    private readonly Lazy<DateTime> _lastAddedDate;
    private readonly Lazy<DateTime?> _lastAirDate;
    private readonly Lazy<decimal> _lowestAniDBRating;
    private readonly Lazy<int> _missingEpisodes;
    private readonly Lazy<int> _missingEpisodesCollecting;
    private readonly Lazy<string> _name;
    private readonly Lazy<IReadOnlySet<string>> _resolutions;
    private readonly Lazy<IReadOnlySet<string>> _filePaths;
    private readonly Lazy<IReadOnlySet<(int year, AnimeSeason season)>> _seasons;
    private readonly Lazy<int> _seriesCount;
    private readonly Lazy<IReadOnlySet<string>> _sharedAudioLanguages;
    private readonly Lazy<IReadOnlySet<string>> _sharedSubtitleLanguages;
    private readonly Lazy<IReadOnlySet<string>> _sharedVideoSources;
    private readonly Lazy<string> _sortingName;
    private readonly Lazy<IReadOnlySet<string>> _subtitleLanguages;
    private readonly Lazy<IReadOnlySet<string>> _tags;
    private readonly Lazy<int> _totalEpisodeCount;
    private readonly Lazy<IReadOnlySet<string>> _videoSources;
    private readonly Lazy<IReadOnlySet<int>> _years;

    public string Name => _name.Value;

    public Func<string> NameDelegate
    {
        init => _name = new Lazy<string>(value);
    }

    public string SortingName => _sortingName.Value;

    public Func<string> SortingNameDelegate
    {
        init => _sortingName = new Lazy<string>(value);
    }

    public int SeriesCount => _seriesCount.Value;

    public Func<int> SeriesCountDelegate
    {
        init => _seriesCount = new Lazy<int>(value);
    }

    public int MissingEpisodes => _missingEpisodes.Value;

    public Func<int> MissingEpisodesDelegate
    {
        init => _missingEpisodes = new Lazy<int>(value);
    }

    public int MissingEpisodesCollecting => _missingEpisodesCollecting.Value;

    public Func<int> MissingEpisodesCollectingDelegate
    {
        init => _missingEpisodesCollecting = new Lazy<int>(value);
    }

    public IReadOnlySet<string> Tags => _tags.Value;

    public Func<IReadOnlySet<string>> TagsDelegate
    {
        init => _tags = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> CustomTags => _customTags.Value;

    public Func<IReadOnlySet<string>> CustomTagsDelegate
    {
        init => _customTags = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<int> Years => _years.Value;

    public Func<IReadOnlySet<int>> YearsDelegate
    {
        init => _years = new Lazy<IReadOnlySet<int>>(value);
    }

    public IReadOnlySet<(int year, AnimeSeason season)> Seasons => _seasons.Value;

    public Func<IReadOnlySet<(int year, AnimeSeason season)>> SeasonsDelegate
    {
        init => _seasons = new Lazy<IReadOnlySet<(int year, AnimeSeason season)>>(value);
    }

    public bool HasTvDBLink => _hasTvDBLink.Value;

    public Func<bool> HasTvDBLinkDelegate
    {
        init => _hasTvDBLink = new Lazy<bool>(value);
    }

    public bool HasMissingTvDbLink => _hasMissingTvDBLink.Value;

    public Func<bool> HasMissingTvDbLinkDelegate
    {
        init => _hasMissingTvDBLink = new Lazy<bool>(value);
    }

    public bool HasTMDbLink => _hasTMDbLink.Value;

    public Func<bool> HasTMDbLinkDelegate
    {
        init => _hasTMDbLink = new Lazy<bool>(value);
    }

    public bool HasMissingTMDbLink => _hasMissingTMDbLink.Value;

    public Func<bool> HasMissingTMDbLinkDelegate
    {
        init => _hasMissingTMDbLink = new Lazy<bool>(value);
    }

    public bool HasTraktLink => _hasTraktLink.Value;

    public Func<bool> HasTraktLinkDelegate
    {
        init => _hasTraktLink = new Lazy<bool>(value);
    }

    public bool HasMissingTraktLink => _hasMissingTraktLink.Value;

    public Func<bool> HasMissingTraktLinkDelegate
    {
        init => _hasMissingTraktLink = new Lazy<bool>(value);
    }

    public bool IsFinished => _isFinished.Value;

    public Func<bool> IsFinishedDelegate
    {
        init => _isFinished = new Lazy<bool>(value);
    }

    public DateTime? AirDate => _airDate.Value;

    public Func<DateTime?> AirDateDelegate
    {
        init => _airDate = new Lazy<DateTime?>(value);
    }

    public DateTime? LastAirDate => _lastAirDate.Value;

    public Func<DateTime?> LastAirDateDelegate
    {
        init => _lastAirDate = new Lazy<DateTime?>(value);
    }

    public DateTime AddedDate => _addedDate.Value;

    public Func<DateTime> AddedDateDelegate
    {
        init => _addedDate = new Lazy<DateTime>(value);
    }

    public DateTime LastAddedDate => _lastAddedDate.Value;

    public Func<DateTime> LastAddedDateDelegate
    {
        init => _lastAddedDate = new Lazy<DateTime>(value);
    }

    public int EpisodeCount => _episodeCount.Value;

    public Func<int> EpisodeCountDelegate
    {
        init => _episodeCount = new Lazy<int>(value);
    }

    public int TotalEpisodeCount => _totalEpisodeCount.Value;

    public Func<int> TotalEpisodeCountDelegate
    {
        init => _totalEpisodeCount = new Lazy<int>(value);
    }

    public decimal LowestAniDBRating => _lowestAniDBRating.Value;

    public Func<decimal> LowestAniDBRatingDelegate
    {
        init => _lowestAniDBRating = new Lazy<decimal>(value);
    }

    public decimal HighestAniDBRating => _highestAniDBRating.Value;

    public Func<decimal> HighestAniDBRatingDelegate
    {
        init => _highestAniDBRating = new Lazy<decimal>(value);
    }

    public decimal AverageAniDBRating => _averageAniDBRating.Value;

    public Func<decimal> AverageAniDBRatingDelegate
    {
        init => _averageAniDBRating = new Lazy<decimal>(value);
    }

    public IReadOnlySet<string> VideoSources => _videoSources.Value;

    public Func<IReadOnlySet<string>> VideoSourcesDelegate
    {
        init => _videoSources = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> SharedVideoSources => _sharedVideoSources.Value;

    public Func<IReadOnlySet<string>> SharedVideoSourcesDelegate
    {
        init => _sharedVideoSources = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> AnimeTypes => _animeTypes.Value;

    public Func<IReadOnlySet<string>> AnimeTypesDelegate
    {
        init => _animeTypes = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> AudioLanguages => _audioLanguages.Value;

    public Func<IReadOnlySet<string>> AudioLanguagesDelegate
    {
        init => _audioLanguages = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> SharedAudioLanguages => _sharedAudioLanguages.Value;

    public Func<IReadOnlySet<string>> SharedAudioLanguagesDelegate
    {
        init => _sharedAudioLanguages = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> SubtitleLanguages => _subtitleLanguages.Value;

    public Func<IReadOnlySet<string>> SubtitleLanguagesDelegate
    {
        init => _subtitleLanguages = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> SharedSubtitleLanguages => _sharedSubtitleLanguages.Value;

    public Func<IReadOnlySet<string>> SharedSubtitleLanguagesDelegate
    {
        init => _sharedSubtitleLanguages = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> Resolutions => _resolutions.Value;
    public Func<IReadOnlySet<string>> ResolutionsDelegate
    {
        init
        {
            _resolutions = new Lazy<IReadOnlySet<string>>(value);
        }
    }
    
    public IReadOnlySet<string> FilePaths => _filePaths.Value;

    public Func<IReadOnlySet<string>> FilePathsDelegate
    {
        init => _filePaths = new Lazy<IReadOnlySet<string>>(value);
    }
}
