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
    }

    public Func<string> NameDelegate
    {
        init
        {
            _nameDelegate = value;
            _name = new Lazy<string>(_nameDelegate, LazyThreadSafetyMode.None);
        }
    }

    public string SortingName => _sortingName.Value;

    public Func<string> SortingNameDelegate
    {
        init
        {
            _sortingNameDelegate = value;
            _sortingName = new Lazy<string>(_sortingNameDelegate, LazyThreadSafetyMode.None);
        }
    }

    public int SeriesCount => _seriesCount.Value;

    public Func<int> SeriesCountDelegate
    {
        init
        {
            _seriesCountDelegate = value;
            _seriesCount = new Lazy<int>(_seriesCountDelegate, LazyThreadSafetyMode.None);
        }
    }

    public int MissingEpisodes => _missingEpisodes.Value;

    public Func<int> MissingEpisodesDelegate
    {
        init
        {
            _missingEpisodesDelegate = value;
            _missingEpisodes = new Lazy<int>(_missingEpisodesDelegate, LazyThreadSafetyMode.None);
        }
    }

    public int MissingEpisodesCollecting => _missingEpisodesCollecting.Value;

    public Func<int> MissingEpisodesCollectingDelegate
    {
        init
        {
            _missingEpisodesCollectingDelegate = value;
            _missingEpisodesCollecting = new Lazy<int>(_missingEpisodesCollectingDelegate, LazyThreadSafetyMode.None);
        }
    }

    public IReadOnlySet<string> Tags => _tags.Value;

    public Func<IReadOnlySet<string>> TagsDelegate
    {
        init
        {
            _tagsDelegate = value;
            _tags = new Lazy<IReadOnlySet<string>>(_tagsDelegate, LazyThreadSafetyMode.None);
        }
    }

    public IReadOnlySet<string> CustomTags => _customTags.Value;

    public Func<IReadOnlySet<string>> CustomTagsDelegate
    {
        init
        {
            _customTagsDelegate = value;
            _customTags = new Lazy<IReadOnlySet<string>>(_customTagsDelegate, LazyThreadSafetyMode.None);
        }
    }

    public IReadOnlySet<int> Years => _years.Value;

    public Func<IReadOnlySet<int>> YearsDelegate
    {
        init
        {
            _yearsDelegate = value;
            _years = new Lazy<IReadOnlySet<int>>(_yearsDelegate, LazyThreadSafetyMode.None);
        }
    }

    public IReadOnlySet<(int year, AnimeSeason season)> Seasons => _seasons.Value;

    public Func<IReadOnlySet<(int year, AnimeSeason season)>> SeasonsDelegate
    {
        init
        {
            _seasonsDelegate = value;
            _seasons = new Lazy<IReadOnlySet<(int year, AnimeSeason season)>>(_seasonsDelegate, LazyThreadSafetyMode.None);
        }
    }

    public bool HasTvDBLink => _hasTvDBLink.Value;

    public Func<bool> HasTvDBLinkDelegate
    {
        init
        {
            _hasTvDBLinkDelegate = value;
            _hasTvDBLink = new Lazy<bool>(_hasTvDBLinkDelegate, LazyThreadSafetyMode.None);
        }
    }

    public bool HasMissingTvDbLink => _hasMissingTvDBLink.Value;

    public Func<bool> HasMissingTvDbLinkDelegate
    {
        init
        {
            _hasMissingTvDbLinkDelegate = value;
            _hasMissingTvDBLink = new Lazy<bool>(_hasMissingTvDbLinkDelegate, LazyThreadSafetyMode.None);
        }
    }

    public bool HasTMDbLink => _hasTMDbLink.Value;

    public Func<bool> HasTMDbLinkDelegate
    {
        init
        {
            _hasTmDbLinkDelegate = value;
            _hasTMDbLink = new Lazy<bool>(_hasTmDbLinkDelegate, LazyThreadSafetyMode.None);
        }
    }

    public bool HasMissingTMDbLink => _hasMissingTMDbLink.Value;

    public Func<bool> HasMissingTMDbLinkDelegate
    {
        init
        {
            _hasMissingTmDbLinkDelegate = value;
            _hasMissingTMDbLink = new Lazy<bool>(_hasMissingTmDbLinkDelegate, LazyThreadSafetyMode.None);
        }
    }

    public bool HasTraktLink => _hasTraktLink.Value;

    public Func<bool> HasTraktLinkDelegate
    {
        init
        {
            _hasTraktLinkDelegate = value;
            _hasTraktLink = new Lazy<bool>(_hasTraktLinkDelegate, LazyThreadSafetyMode.None);
        }
    }

    public bool HasMissingTraktLink => _hasMissingTraktLink.Value;

    public Func<bool> HasMissingTraktLinkDelegate
    {
        init
        {
            _hasMissingTraktLinkDelegate = value;
            _hasMissingTraktLink = new Lazy<bool>(_hasMissingTraktLinkDelegate, LazyThreadSafetyMode.None);
        }
    }

    public bool IsFinished => _isFinished.Value;

    public Func<bool> IsFinishedDelegate
    {
        init
        {
            _isFinishedDelegate = value;
            _isFinished = new Lazy<bool>(_isFinishedDelegate, LazyThreadSafetyMode.None);
        }
    }

    public DateTime? AirDate => _airDate.Value;

    public Func<DateTime?> AirDateDelegate
    {
        init
        {
            _airDateDelegate = value;
            _airDate = new Lazy<DateTime?>(_airDateDelegate, LazyThreadSafetyMode.None);
        }
    }

    public DateTime? LastAirDate => _lastAirDate.Value;

    public Func<DateTime?> LastAirDateDelegate
    {
        init
        {
            _lastAirDateDelegate = value;
            _lastAirDate = new Lazy<DateTime?>(_lastAirDateDelegate, LazyThreadSafetyMode.None);
        }
    }

    public DateTime AddedDate => _addedDate.Value;

    public Func<DateTime> AddedDateDelegate
    {
        init
        {
            _addedDateDelegate = value;
            _addedDate = new Lazy<DateTime>(_addedDateDelegate, LazyThreadSafetyMode.None);
        }
    }

    public DateTime LastAddedDate => _lastAddedDate.Value;

    public Func<DateTime> LastAddedDateDelegate
    {
        init
        {
            _lastAddedDateDelegate = value;
            _lastAddedDate = new Lazy<DateTime>(_lastAddedDateDelegate, LazyThreadSafetyMode.None);
        }
    }

    public int EpisodeCount => _episodeCount.Value;

    public Func<int> EpisodeCountDelegate
    {
        init
        {
            _episodeCountDelegate = value;
            _episodeCount = new Lazy<int>(_episodeCountDelegate, LazyThreadSafetyMode.None);
        }
    }

    public int TotalEpisodeCount => _totalEpisodeCount.Value;

    public Func<int> TotalEpisodeCountDelegate
    {
        init
        {
            _totalEpisodeCountDelegate = value;
            _totalEpisodeCount = new Lazy<int>(_totalEpisodeCountDelegate, LazyThreadSafetyMode.None);
        }
    }

    public decimal LowestAniDBRating => _lowestAniDBRating.Value;

    public Func<decimal> LowestAniDBRatingDelegate
    {
        init
        {
            _lowestAniDBRatingDelegate = value;
            _lowestAniDBRating = new Lazy<decimal>(_lowestAniDBRatingDelegate, LazyThreadSafetyMode.None);
        }
    }

    public decimal HighestAniDBRating => _highestAniDBRating.Value;

    public Func<decimal> HighestAniDBRatingDelegate
    {
        init
        {
            _highestAniDBRatingDelegate = value;
            _highestAniDBRating = new Lazy<decimal>(_highestAniDBRatingDelegate, LazyThreadSafetyMode.None);
        }
    }

    public decimal AverageAniDBRating => _averageAniDBRating.Value;

    public Func<decimal> AverageAniDBRatingDelegate
    {
        init
        {
            _averageAniDBRatingDelegate = value;
            _averageAniDBRating = new Lazy<decimal>(_averageAniDBRatingDelegate, LazyThreadSafetyMode.None);
        }
    }

    public IReadOnlySet<string> VideoSources => _videoSources.Value;

    public Func<IReadOnlySet<string>> VideoSourcesDelegate
    {
        init
        {
            _videoSourcesDelegate = value;
            _videoSources = new Lazy<IReadOnlySet<string>>(_videoSourcesDelegate, LazyThreadSafetyMode.None);
        }
    }

    public IReadOnlySet<string> SharedVideoSources => _sharedVideoSources.Value;

    public Func<IReadOnlySet<string>> SharedVideoSourcesDelegate
    {
        init
        {
            _sharedVideoSourcesDelegate = value;
            _sharedVideoSources = new Lazy<IReadOnlySet<string>>(_sharedVideoSourcesDelegate, LazyThreadSafetyMode.None);
        }
    }

    public IReadOnlySet<string> AnimeTypes => _animeTypes.Value;

    public Func<IReadOnlySet<string>> AnimeTypesDelegate
    {
        init
        {
            _animeTypesDelegate = value;
            _animeTypes = new Lazy<IReadOnlySet<string>>(_animeTypesDelegate, LazyThreadSafetyMode.None);
        }
    }

    public IReadOnlySet<string> AudioLanguages => _audioLanguages.Value;

    public Func<IReadOnlySet<string>> AudioLanguagesDelegate
    {
        init
        {
            _audioLanguagesDelegate = value;
            _audioLanguages = new Lazy<IReadOnlySet<string>>(_audioLanguagesDelegate, LazyThreadSafetyMode.None);
        }
    }

    public IReadOnlySet<string> SharedAudioLanguages => _sharedAudioLanguages.Value;

    public Func<IReadOnlySet<string>> SharedAudioLanguagesDelegate
    {
        init
        {
            _sharedAudioLanguagesDelegate = value;
            _sharedAudioLanguages = new Lazy<IReadOnlySet<string>>(_sharedAudioLanguagesDelegate, LazyThreadSafetyMode.None);
        }
    }

    public IReadOnlySet<string> SubtitleLanguages => _subtitleLanguages.Value;

    public Func<IReadOnlySet<string>> SubtitleLanguagesDelegate
    {
        init
        {
            _subtitleLanguagesDelegate = value;
            _subtitleLanguages = new Lazy<IReadOnlySet<string>>(_subtitleLanguagesDelegate, LazyThreadSafetyMode.None);
        }
    }

    public IReadOnlySet<string> SharedSubtitleLanguages => _sharedSubtitleLanguages.Value;

    public Func<IReadOnlySet<string>> SharedSubtitleLanguagesDelegate
    {
        init
        {
            _sharedSubtitleLanguagesDelegate = value;
            _sharedSubtitleLanguages = new Lazy<IReadOnlySet<string>>(_sharedSubtitleLanguagesDelegate, LazyThreadSafetyMode.None);
        }
    }
}
