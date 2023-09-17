using System;
using System.Collections.Generic;
using System.Threading;
using Shoko.Models.Enums;

namespace Shoko.Server.Filters;

public class Filterable
{

    private readonly Lazy<DateTime> _addedDate;
    private readonly Func<DateTime> _addedDateDelegate;

    private readonly Lazy<DateTime?> _airDate;
    private readonly Func<DateTime?> _airDateDelegate;

    private readonly Lazy<IReadOnlySet<string>> _animeTypes;
    private readonly Func<IReadOnlySet<string>> _animeTypesDelegate;

    private readonly Lazy<IReadOnlySet<string>> _audioLanguages;
    private readonly Func<IReadOnlySet<string>> _audioLanguagesDelegate;

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

    /// <summary>
    /// Name
    /// </summary>
    public string Name => _name.Value;

    public Func<string> NameDelegate
    {
        get => _nameDelegate;
        init
        {
            _nameDelegate = value;
            _name = new Lazy<string>(_nameDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// Sorting Name
    /// </summary>
    public string SortingName => _sortingName.Value;

    public Func<string> SortingNameDelegate
    {
        get => _sortingNameDelegate;
        init
        {
            _sortingNameDelegate = value;
            _sortingName = new Lazy<string>(_sortingNameDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// The number of series in a group
    /// </summary>
    public int SeriesCount => _seriesCount.Value;

    public Func<int> SeriesCountDelegate
    {
        get => _seriesCountDelegate;
        init
        {
            _seriesCountDelegate = value;
            _seriesCount = new Lazy<int>(_seriesCountDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// Number of Missing Episodes
    /// </summary>
    public int MissingEpisodes => _missingEpisodes.Value;

    public Func<int> MissingEpisodesDelegate
    {
        get => _missingEpisodesDelegate;
        init
        {
            _missingEpisodesDelegate = value;
            _missingEpisodes = new Lazy<int>(_missingEpisodesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// Number of Missing Episodes from Groups that you have
    /// </summary>
    public int MissingEpisodesCollecting => _missingEpisodesCollecting.Value;

    public Func<int> MissingEpisodesCollectingDelegate
    {
        get => _missingEpisodesCollectingDelegate;
        init
        {
            _missingEpisodesCollectingDelegate = value;
            _missingEpisodesCollecting = new Lazy<int>(_missingEpisodesCollectingDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// All of the tags
    /// </summary>
    public IReadOnlySet<string> Tags => _tags.Value;

    public Func<IReadOnlySet<string>> TagsDelegate
    {
        get => _tagsDelegate;
        init
        {
            _tagsDelegate = value;
            _tags = new Lazy<IReadOnlySet<string>>(_tagsDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// All of the custom tags
    /// </summary>
    public IReadOnlySet<string> CustomTags => _customTags.Value;

    public Func<IReadOnlySet<string>> CustomTagsDelegate
    {
        get => _customTagsDelegate;
        init
        {
            _customTagsDelegate = value;
            _customTags = new Lazy<IReadOnlySet<string>>(_customTagsDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// The years this aired in
    /// </summary>
    public IReadOnlySet<int> Years => _years.Value;

    public Func<IReadOnlySet<int>> YearsDelegate
    {
        get => _yearsDelegate;
        init
        {
            _yearsDelegate = value;
            _years = new Lazy<IReadOnlySet<int>>(_yearsDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// The seasons this aired in
    /// </summary>
    public IReadOnlySet<(int year, AnimeSeason season)> Seasons => _seasons.Value;

    public Func<IReadOnlySet<(int year, AnimeSeason season)>> SeasonsDelegate
    {
        get => _seasonsDelegate;
        init
        {
            _seasonsDelegate = value;
            _seasons = new Lazy<IReadOnlySet<(int year, AnimeSeason season)>>(_seasonsDelegate);
        }
    }

    /// <summary>
    /// Has at least one TvDB Link
    /// </summary>
    public bool HasTvDBLink => _hasTvDBLink.Value;

    public Func<bool> HasTvDBLinkDelegate
    {
        get => _hasTvDBLinkDelegate;
        init
        {
            _hasTvDBLinkDelegate = value;
            _hasTvDBLink = new Lazy<bool>(_hasTvDBLinkDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// Missing at least one TvDB Link
    /// </summary>
    public bool HasMissingTvDbLink => _hasMissingTvDBLink.Value;

    public Func<bool> HasMissingTvDbLinkDelegate
    {
        get => _hasMissingTvDbLinkDelegate;
        init
        {
            _hasMissingTvDbLinkDelegate = value;
            _hasMissingTvDBLink = new Lazy<bool>(_hasMissingTvDbLinkDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// Has at least one TMDb Link
    /// </summary>
    public bool HasTMDbLink => _hasTMDbLink.Value;

    public Func<bool> HasTMDbLinkDelegate
    {
        get => _hasTmDbLinkDelegate;
        init
        {
            _hasTmDbLinkDelegate = value;
            _hasTMDbLink = new Lazy<bool>(_hasTmDbLinkDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// Missing at least one TMDb Link
    /// </summary>
    public bool HasMissingTMDbLink => _hasMissingTMDbLink.Value;

    public Func<bool> HasMissingTMDbLinkDelegate
    {
        get => _hasMissingTmDbLinkDelegate;
        init
        {
            _hasMissingTmDbLinkDelegate = value;
            _hasMissingTMDbLink = new Lazy<bool>(_hasMissingTmDbLinkDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// Has at least one Trakt Link
    /// </summary>
    public bool HasTraktLink => _hasTraktLink.Value;

    public Func<bool> HasTraktLinkDelegate
    {
        get => _hasTraktLinkDelegate;
        init
        {
            _hasTraktLinkDelegate = value;
            _hasTraktLink = new Lazy<bool>(_hasTraktLinkDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// Missing at least one Trakt Link
    /// </summary>
    public bool HasMissingTraktLink => _hasMissingTraktLink.Value;

    public Func<bool> HasMissingTraktLinkDelegate
    {
        get => _hasMissingTraktLinkDelegate;
        init
        {
            _hasMissingTraktLinkDelegate = value;
            _hasMissingTraktLink = new Lazy<bool>(_hasMissingTraktLinkDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// Has Finished airing
    /// </summary>
    public bool IsFinished => _isFinished.Value;

    public Func<bool> IsFinishedDelegate
    {
        get => _isFinishedDelegate;
        init
        {
            _isFinishedDelegate = value;
            _isFinished = new Lazy<bool>(_isFinishedDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// First Air Date
    /// </summary>
    public DateTime? AirDate => _airDate.Value;

    public Func<DateTime?> AirDateDelegate
    {
        get => _airDateDelegate;
        init
        {
            _airDateDelegate = value;
            _airDate = new Lazy<DateTime?>(_airDateDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// Latest Air Date
    /// </summary>
    public DateTime? LastAirDate => _lastAirDate.Value;

    public Func<DateTime?> LastAirDateDelegate
    {
        get => _lastAirDateDelegate;
        init
        {
            _lastAirDateDelegate = value;
            _lastAirDate = new Lazy<DateTime?>(_lastAirDateDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// When it was first added to the collection
    /// </summary>
    public DateTime AddedDate => _addedDate.Value;

    public Func<DateTime> AddedDateDelegate
    {
        get => _addedDateDelegate;
        init
        {
            _addedDateDelegate = value;
            _addedDate = new Lazy<DateTime>(_addedDateDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// When it was most recently added to the collection
    /// </summary>
    public DateTime LastAddedDate => _lastAddedDate.Value;

    public Func<DateTime> LastAddedDateDelegate
    {
        get => _lastAddedDateDelegate;
        init
        {
            _lastAddedDateDelegate = value;
            _lastAddedDate = new Lazy<DateTime>(_lastAddedDateDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// Highest Episode Count
    /// </summary>
    public int EpisodeCount => _episodeCount.Value;

    public Func<int> EpisodeCountDelegate
    {
        get => _episodeCountDelegate;
        init
        {
            _episodeCountDelegate = value;
            _episodeCount = new Lazy<int>(_episodeCountDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// Total Episode Count
    /// </summary>
    public int TotalEpisodeCount => _totalEpisodeCount.Value;

    public Func<int> TotalEpisodeCountDelegate
    {
        get => _totalEpisodeCountDelegate;
        init
        {
            _totalEpisodeCountDelegate = value;
            _totalEpisodeCount = new Lazy<int>(_totalEpisodeCountDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// Lowest AniDB Rating (0-10)
    /// </summary>
    public decimal LowestAniDBRating => _lowestAniDBRating.Value;

    public Func<decimal> LowestAniDBRatingDelegate
    {
        get => _lowestAniDBRatingDelegate;
        init
        {
            _lowestAniDBRatingDelegate = value;
            _lowestAniDBRating = new Lazy<decimal>(_lowestAniDBRatingDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// Highest AniDB Rating (0-10)
    /// </summary>
    public decimal HighestAniDBRating => _highestAniDBRating.Value;

    public Func<decimal> HighestAniDBRatingDelegate
    {
        get => _highestAniDBRatingDelegate;
        init
        {
            _highestAniDBRatingDelegate = value;
            _highestAniDBRating = new Lazy<decimal>(_highestAniDBRatingDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// The sources that the video came from, such as TV, Web, DVD, Blu-ray, etc.
    /// </summary>
    public IReadOnlySet<string> VideoSources => _videoSources.Value;

    public Func<IReadOnlySet<string>> VideoSourcesDelegate
    {
        get => _videoSourcesDelegate;
        init
        {
            _videoSourcesDelegate = value;
            _videoSources = new Lazy<IReadOnlySet<string>>(_videoSourcesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// The sources that the video came from, such as TV, Web, DVD, Blu-ray, etc. (only sources that are in every file)
    /// </summary>
    public IReadOnlySet<string> SharedVideoSources => _sharedVideoSources.Value;

    public Func<IReadOnlySet<string>> SharedVideoSourcesDelegate
    {
        get => _sharedVideoSourcesDelegate;
        init
        {
            _sharedVideoSourcesDelegate = value;
            _sharedVideoSources = new Lazy<IReadOnlySet<string>>(_sharedVideoSourcesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// The anime types (movie, series, ova, etc)
    /// </summary>
    public IReadOnlySet<string> AnimeTypes => _animeTypes.Value;

    public Func<IReadOnlySet<string>> AnimeTypesDelegate
    {
        get => _animeTypesDelegate;
        init
        {
            _animeTypesDelegate = value;
            _animeTypes = new Lazy<IReadOnlySet<string>>(_animeTypesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// Audio Languages
    /// </summary>
    public IReadOnlySet<string> AudioLanguages => _audioLanguages.Value;

    public Func<IReadOnlySet<string>> AudioLanguagesDelegate
    {
        get => _audioLanguagesDelegate;
        init
        {
            _audioLanguagesDelegate = value;
            _audioLanguages = new Lazy<IReadOnlySet<string>>(_audioLanguagesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// Audio Languages (only languages that are in every file)
    /// </summary>
    public IReadOnlySet<string> SharedAudioLanguages => _sharedAudioLanguages.Value;

    public Func<IReadOnlySet<string>> SharedAudioLanguagesDelegate
    {
        get => _sharedAudioLanguagesDelegate;
        init
        {
            _sharedAudioLanguagesDelegate = value;
            _sharedAudioLanguages = new Lazy<IReadOnlySet<string>>(_sharedAudioLanguagesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// Subtitle Languages
    /// </summary>
    public IReadOnlySet<string> SubtitleLanguages => _subtitleLanguages.Value;

    public Func<IReadOnlySet<string>> SubtitleLanguagesDelegate
    {
        get => _subtitleLanguagesDelegate;
        init
        {
            _subtitleLanguagesDelegate = value;
            _subtitleLanguages = new Lazy<IReadOnlySet<string>>(_subtitleLanguagesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    /// Subtitle Languages (only languages that are in every file)
    /// </summary>
    public IReadOnlySet<string> SharedSubtitleLanguages => _sharedSubtitleLanguages.Value;

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
