using System;
using System.Collections.Generic;
using System.Threading;
using Shoko.Models.Enums;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters;

public class Filterable : IFilterable
{

    private readonly Lazy<DateTime> _addedDate;
    private readonly Func<DateTime> _addedDateDelegate;

    private readonly Lazy<DateTime?> _airDate;
    private readonly Func<DateTime?> _airDateDelegate;

    private readonly Lazy<IReadOnlySet<string>> _animeTypes;
    private readonly Func<IReadOnlySet<string>> _animeTypesDelegate;

    private readonly Lazy<IReadOnlySet<string>> _audioLanguages;
    private readonly Func<IReadOnlySet<string>> _audioLanguagesDelegate;
    
    private readonly Lazy<decimal> _averageAniDBRating;
    private readonly Func<decimal> _averageAniDBRatingDelegate;

    private readonly Lazy<IReadOnlySet<string>> _customTags;
    private readonly Func<IReadOnlySet<string>> _customTagsDelegate;

    private readonly Lazy<int> _episodeCount;
    private readonly Func<int> _episodeCountDelegate;

    private readonly Lazy<bool> _hasMissingTMDbLink;
    private readonly Func<bool> _hasMissingTmDbLinkDelegate;

    private readonly Lazy<bool> _hasMissingTraktLink;
    private readonly Func<bool> _hasMissingTraktLinkDelegate;

    private readonly Lazy<bool> _hasMissingTvDBLink;
    private readonly Func<bool> _hasMissingTvDbLinkDelegate;

    private readonly Lazy<bool> _hasTMDbLink;
    private readonly Func<bool> _hasTmDbLinkDelegate;

    private readonly Lazy<bool> _hasTraktLink;
    private readonly Func<bool> _hasTraktLinkDelegate;

    private readonly Lazy<bool> _hasTvDBLink;
    private readonly Func<bool> _hasTvDBLinkDelegate;

    private readonly Lazy<decimal> _highestAniDBRating;
    private readonly Func<decimal> _highestAniDBRatingDelegate;

    private readonly Lazy<bool> _isFinished;
    private readonly Func<bool> _isFinishedDelegate;

    private readonly Lazy<DateTime> _lastAddedDate;
    private readonly Func<DateTime> _lastAddedDateDelegate;

    private readonly Lazy<DateTime?> _lastAirDate;
    private readonly Func<DateTime?> _lastAirDateDelegate;

    private readonly Lazy<decimal> _lowestAniDBRating;
    private readonly Func<decimal> _lowestAniDBRatingDelegate;

    private readonly Lazy<int> _missingEpisodes;

    private readonly Lazy<int> _missingEpisodesCollecting;
    private readonly Func<int> _missingEpisodesCollectingDelegate;
    private readonly Func<int> _missingEpisodesDelegate;

    private readonly Lazy<string> _name;
    private readonly Func<string> _nameDelegate;

    private readonly Lazy<IReadOnlySet<(int year, AnimeSeason season)>> _seasons;
    private readonly Func<IReadOnlySet<(int year, AnimeSeason season)>> _seasonsDelegate;

    private readonly Lazy<int> _seriesCount;
    private readonly Func<int> _seriesCountDelegate;

    private readonly Lazy<IReadOnlySet<string>> _sharedAudioLanguages;
    private readonly Func<IReadOnlySet<string>> _sharedAudioLanguagesDelegate;

    private readonly Lazy<IReadOnlySet<string>> _sharedSubtitleLanguages;
    private readonly Func<IReadOnlySet<string>> _sharedSubtitleLanguagesDelegate;

    private readonly Lazy<IReadOnlySet<string>> _sharedVideoSources;
    private readonly Func<IReadOnlySet<string>> _sharedVideoSourcesDelegate;

    private readonly Lazy<string> _sortingName;
    private readonly Func<string> _sortingNameDelegate;

    private readonly Lazy<IReadOnlySet<string>> _subtitleLanguages;
    private readonly Func<IReadOnlySet<string>> _subtitleLanguagesDelegate;

    private readonly Lazy<IReadOnlySet<string>> _tags;
    private readonly Func<IReadOnlySet<string>> _tagsDelegate;

    private readonly Lazy<int> _totalEpisodeCount;
    private readonly Func<int> _totalEpisodeCountDelegate;

    private readonly Lazy<IReadOnlySet<string>> _videoSources;
    private readonly Func<IReadOnlySet<string>> _videoSourcesDelegate;

    private readonly Lazy<IReadOnlySet<int>> _years;
    private readonly Func<IReadOnlySet<int>> _yearsDelegate;

    public string Name
    {
        get => _name.Value;
        init => throw new NotSupportedException();
    }

    public Func<string> NameDelegate
    {
        get => _nameDelegate;
        init
        {
            _nameDelegate = value;
            _name = new Lazy<string>(_nameDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public string SortingName
    {
        get => _sortingName.Value;
        init => throw new NotSupportedException();
    }

    public Func<string> SortingNameDelegate
    {
        get => _sortingNameDelegate;
        init
        {
            _sortingNameDelegate = value;
            _sortingName = new Lazy<string>(_sortingNameDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public int SeriesCount
    {
        get => _seriesCount.Value;
        init => throw new NotSupportedException();
    }

    public Func<int> SeriesCountDelegate
    {
        get => _seriesCountDelegate;
        init
        {
            _seriesCountDelegate = value;
            _seriesCount = new Lazy<int>(_seriesCountDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public int MissingEpisodes
    {
        get => _missingEpisodes.Value;
        init => throw new NotSupportedException();
    }

    public Func<int> MissingEpisodesDelegate
    {
        get => _missingEpisodesDelegate;
        init
        {
            _missingEpisodesDelegate = value;
            _missingEpisodes = new Lazy<int>(_missingEpisodesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public int MissingEpisodesCollecting
    {
        get => _missingEpisodesCollecting.Value;
        init => throw new NotSupportedException();
    }

    public Func<int> MissingEpisodesCollectingDelegate
    {
        get => _missingEpisodesCollectingDelegate;
        init
        {
            _missingEpisodesCollectingDelegate = value;
            _missingEpisodesCollecting = new Lazy<int>(_missingEpisodesCollectingDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public IReadOnlySet<string> Tags
    {
        get => _tags.Value;
        init => throw new NotSupportedException();
    }

    public Func<IReadOnlySet<string>> TagsDelegate
    {
        get => _tagsDelegate;
        init
        {
            _tagsDelegate = value;
            _tags = new Lazy<IReadOnlySet<string>>(_tagsDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public IReadOnlySet<string> CustomTags
    {
        get => _customTags.Value;
        init => throw new NotSupportedException();
    }

    public Func<IReadOnlySet<string>> CustomTagsDelegate
    {
        get => _customTagsDelegate;
        init
        {
            _customTagsDelegate = value;
            _customTags = new Lazy<IReadOnlySet<string>>(_customTagsDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public IReadOnlySet<int> Years
    {
        get => _years.Value;
        init => throw new NotSupportedException();
    }

    public Func<IReadOnlySet<int>> YearsDelegate
    {
        get => _yearsDelegate;
        init
        {
            _yearsDelegate = value;
            _years = new Lazy<IReadOnlySet<int>>(_yearsDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public IReadOnlySet<(int year, AnimeSeason season)> Seasons
    {
        get => _seasons.Value;
        init => throw new NotSupportedException();
    }

    public Func<IReadOnlySet<(int year, AnimeSeason season)>> SeasonsDelegate
    {
        get => _seasonsDelegate;
        init
        {
            _seasonsDelegate = value;
            _seasons = new Lazy<IReadOnlySet<(int year, AnimeSeason season)>>(_seasonsDelegate);
        }
    }

    public bool HasTvDBLink
    {
        get => _hasTvDBLink.Value;
        init => throw new NotSupportedException();
    }

    public Func<bool> HasTvDBLinkDelegate
    {
        get => _hasTvDBLinkDelegate;
        init
        {
            _hasTvDBLinkDelegate = value;
            _hasTvDBLink = new Lazy<bool>(_hasTvDBLinkDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public bool HasMissingTvDbLink
    {
        get => _hasMissingTvDBLink.Value;
        init => throw new NotSupportedException();
    }

    public Func<bool> HasMissingTvDbLinkDelegate
    {
        get => _hasMissingTvDbLinkDelegate;
        init
        {
            _hasMissingTvDbLinkDelegate = value;
            _hasMissingTvDBLink = new Lazy<bool>(_hasMissingTvDbLinkDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public bool HasTMDbLink
    {
        get => _hasTMDbLink.Value;
        init => throw new NotSupportedException();
    }

    public Func<bool> HasTMDbLinkDelegate
    {
        get => _hasTmDbLinkDelegate;
        init
        {
            _hasTmDbLinkDelegate = value;
            _hasTMDbLink = new Lazy<bool>(_hasTmDbLinkDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public bool HasMissingTMDbLink
    {
        get => _hasMissingTMDbLink.Value;
        init => throw new NotSupportedException();
    }

    public Func<bool> HasMissingTMDbLinkDelegate
    {
        get => _hasMissingTmDbLinkDelegate;
        init
        {
            _hasMissingTmDbLinkDelegate = value;
            _hasMissingTMDbLink = new Lazy<bool>(_hasMissingTmDbLinkDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public bool HasTraktLink
    {
        get => _hasTraktLink.Value;
        init => throw new NotSupportedException();
    }

    public Func<bool> HasTraktLinkDelegate
    {
        get => _hasTraktLinkDelegate;
        init
        {
            _hasTraktLinkDelegate = value;
            _hasTraktLink = new Lazy<bool>(_hasTraktLinkDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public bool HasMissingTraktLink
    {
        get => _hasMissingTraktLink.Value;
        init => throw new NotSupportedException();
    }

    public Func<bool> HasMissingTraktLinkDelegate
    {
        get => _hasMissingTraktLinkDelegate;
        init
        {
            _hasMissingTraktLinkDelegate = value;
            _hasMissingTraktLink = new Lazy<bool>(_hasMissingTraktLinkDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public bool IsFinished
    {
        get => _isFinished.Value;
        init => throw new NotSupportedException();
    }

    public Func<bool> IsFinishedDelegate
    {
        get => _isFinishedDelegate;
        init
        {
            _isFinishedDelegate = value;
            _isFinished = new Lazy<bool>(_isFinishedDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public DateTime? AirDate
    {
        get => _airDate.Value;
        init => throw new NotSupportedException();
    }

    public Func<DateTime?> AirDateDelegate
    {
        get => _airDateDelegate;
        init
        {
            _airDateDelegate = value;
            _airDate = new Lazy<DateTime?>(_airDateDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public DateTime? LastAirDate
    {
        get => _lastAirDate.Value;
        init => throw new NotSupportedException();
    }

    public Func<DateTime?> LastAirDateDelegate
    {
        get => _lastAirDateDelegate;
        init
        {
            _lastAirDateDelegate = value;
            _lastAirDate = new Lazy<DateTime?>(_lastAirDateDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public DateTime AddedDate
    {
        get => _addedDate.Value;
        init => throw new NotSupportedException();
    }

    public Func<DateTime> AddedDateDelegate
    {
        get => _addedDateDelegate;
        init
        {
            _addedDateDelegate = value;
            _addedDate = new Lazy<DateTime>(_addedDateDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public DateTime LastAddedDate
    {
        get => _lastAddedDate.Value;
        init => throw new NotSupportedException();
    }

    public Func<DateTime> LastAddedDateDelegate
    {
        get => _lastAddedDateDelegate;
        init
        {
            _lastAddedDateDelegate = value;
            _lastAddedDate = new Lazy<DateTime>(_lastAddedDateDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public int EpisodeCount
    {
        get => _episodeCount.Value;
        init => throw new NotSupportedException();
    }

    public Func<int> EpisodeCountDelegate
    {
        get => _episodeCountDelegate;
        init
        {
            _episodeCountDelegate = value;
            _episodeCount = new Lazy<int>(_episodeCountDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public int TotalEpisodeCount
    {
        get => _totalEpisodeCount.Value;
        init => throw new NotSupportedException();
    }

    public Func<int> TotalEpisodeCountDelegate
    {
        get => _totalEpisodeCountDelegate;
        init
        {
            _totalEpisodeCountDelegate = value;
            _totalEpisodeCount = new Lazy<int>(_totalEpisodeCountDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public decimal LowestAniDBRating
    {
        get => _lowestAniDBRating.Value;
        init => throw new NotSupportedException();
    }

    public Func<decimal> LowestAniDBRatingDelegate
    {
        get => _lowestAniDBRatingDelegate;
        init
        {
            _lowestAniDBRatingDelegate = value;
            _lowestAniDBRating = new Lazy<decimal>(_lowestAniDBRatingDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public decimal HighestAniDBRating
    {
        get => _highestAniDBRating.Value;
        init => throw new NotSupportedException();
    }

    public Func<decimal> HighestAniDBRatingDelegate
    {
        get => _highestAniDBRatingDelegate;
        init
        {
            _highestAniDBRatingDelegate = value;
            _highestAniDBRating = new Lazy<decimal>(_highestAniDBRatingDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public decimal AverageAniDBRating
    {
        get => _averageAniDBRating.Value;
        init => throw new NotSupportedException();
    }

    public Func<decimal> AverageAniDBRatingDelegate
    {
        get => _averageAniDBRatingDelegate;
        init
        {
            _averageAniDBRatingDelegate = value;
            _averageAniDBRating = new Lazy<decimal>(_averageAniDBRatingDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public IReadOnlySet<string> VideoSources
    {
        get => _videoSources.Value;
        init => throw new NotSupportedException();
    }

    public Func<IReadOnlySet<string>> VideoSourcesDelegate
    {
        get => _videoSourcesDelegate;
        init
        {
            _videoSourcesDelegate = value;
            _videoSources = new Lazy<IReadOnlySet<string>>(_videoSourcesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public IReadOnlySet<string> SharedVideoSources
    {
        get => _sharedVideoSources.Value;
        init => throw new NotSupportedException();
    }

    public Func<IReadOnlySet<string>> SharedVideoSourcesDelegate
    {
        get => _sharedVideoSourcesDelegate;
        init
        {
            _sharedVideoSourcesDelegate = value;
            _sharedVideoSources = new Lazy<IReadOnlySet<string>>(_sharedVideoSourcesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public IReadOnlySet<string> AnimeTypes
    {
        get => _animeTypes.Value;
        init => throw new NotSupportedException();
    }

    public Func<IReadOnlySet<string>> AnimeTypesDelegate
    {
        get => _animeTypesDelegate;
        init
        {
            _animeTypesDelegate = value;
            _animeTypes = new Lazy<IReadOnlySet<string>>(_animeTypesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public IReadOnlySet<string> AudioLanguages
    {
        get => _audioLanguages.Value;
        init => throw new NotSupportedException();
    }

    public Func<IReadOnlySet<string>> AudioLanguagesDelegate
    {
        get => _audioLanguagesDelegate;
        init
        {
            _audioLanguagesDelegate = value;
            _audioLanguages = new Lazy<IReadOnlySet<string>>(_audioLanguagesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public IReadOnlySet<string> SharedAudioLanguages
    {
        get => _sharedAudioLanguages.Value;
        init => throw new NotSupportedException();
    }

    public Func<IReadOnlySet<string>> SharedAudioLanguagesDelegate
    {
        get => _sharedAudioLanguagesDelegate;
        init
        {
            _sharedAudioLanguagesDelegate = value;
            _sharedAudioLanguages = new Lazy<IReadOnlySet<string>>(_sharedAudioLanguagesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public IReadOnlySet<string> SubtitleLanguages
    {
        get => _subtitleLanguages.Value;
        init => throw new NotSupportedException();
    }

    public Func<IReadOnlySet<string>> SubtitleLanguagesDelegate
    {
        get => _subtitleLanguagesDelegate;
        init
        {
            _subtitleLanguagesDelegate = value;
            _subtitleLanguages = new Lazy<IReadOnlySet<string>>(_subtitleLanguagesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public IReadOnlySet<string> SharedSubtitleLanguages
    {
        get => _sharedSubtitleLanguages.Value;
        init => throw new NotSupportedException();
    }

    public Func<IReadOnlySet<string>> SharedSubtitleLanguagesDelegate
    {
        get => _sharedSubtitleLanguagesDelegate;
        init
        {
            _sharedSubtitleLanguagesDelegate = value;
            _sharedSubtitleLanguages = new Lazy<IReadOnlySet<string>>(_sharedSubtitleLanguagesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }
}
