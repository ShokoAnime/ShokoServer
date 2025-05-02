using System;
using System.Collections.Generic;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Filters.Interfaces;
using Shoko.Server.Server;

namespace Shoko.Server.Filters;

public class Filterable : IFilterable
{
    private readonly Lazy<DateTime> _addedDate;
    private readonly Lazy<DateTime?> _airDate;
    private readonly Lazy<IReadOnlySet<string>> _animeTypes;
    private readonly Lazy<IReadOnlySet<string>> _audioLanguages;
    private readonly Lazy<decimal> _averageAniDBRating;
    private readonly Lazy<IReadOnlySet<string>> _customTagIDs;
    private readonly Lazy<IReadOnlySet<string>> _customTags;
    private readonly Lazy<int> _episodeCount;
    private readonly Lazy<bool> _hasTmdbAutoLinkingDisabled;
    private readonly Lazy<bool> _hasTraktAutoLinkingDisabled;
    private readonly Lazy<bool> _hasTmdbLink;
    private readonly Lazy<int> _automaticTmdbEpisodeLinks;
    private readonly Lazy<int> _userVerifiedTmdbEpisodeLinks;
    private readonly Lazy<bool> _hasTraktLink;
    private readonly Lazy<decimal> _highestAniDBRating;
    private readonly Lazy<bool> _isFinished;
    private readonly Lazy<DateTime> _lastAddedDate;
    private readonly Lazy<DateTime?> _lastAirDate;
    private readonly Lazy<decimal> _lowestAniDBRating;
    private readonly Lazy<int> _missingEpisodes;
    private readonly Lazy<int> _missingEpisodesCollecting;
    private readonly Lazy<int> _videoFiles;
    private readonly Lazy<string> _name;
    private readonly Lazy<IReadOnlySet<string>> _names;
    private readonly Lazy<IReadOnlySet<string>> _aniDbIds;
    private readonly Lazy<IReadOnlySet<string>> _resolutions;
    private readonly Lazy<IReadOnlySet<string>> _managedFolderIDs;
    private readonly Lazy<IReadOnlySet<string>> _managedFolderNames;
    private readonly Lazy<IReadOnlySet<string>> _filePaths;
    private readonly Lazy<IReadOnlySet<(int year, AnimeSeason season)>> _seasons;
    private readonly Lazy<int> _seriesCount;
    private readonly Lazy<IReadOnlySet<string>> _sharedAudioLanguages;
    private readonly Lazy<IReadOnlySet<string>> _sharedSubtitleLanguages;
    private readonly Lazy<IReadOnlySet<string>> _sharedVideoSources;
    private readonly Lazy<string> _sortingName;
    private readonly Lazy<IReadOnlySet<string>> _subtitleLanguages;
    private readonly Lazy<IReadOnlySet<string>> _anidbTagIDs;
    private readonly Lazy<IReadOnlySet<string>> _anidbTags;
    private readonly Lazy<int> _totalEpisodeCount;
    private readonly Lazy<IReadOnlySet<string>> _videoSources;
    private readonly Lazy<IReadOnlySet<int>> _years;
    private readonly Lazy<IReadOnlySet<ImageEntityType>> _availableImageTypes;
    private readonly Lazy<IReadOnlySet<ImageEntityType>> _preferredImageTypes;
    private readonly Lazy<IReadOnlySet<string>> _characterIDs;
    private readonly Lazy<IReadOnlyDictionary<CharacterAppearanceType, IReadOnlySet<string>>> _characterAppearances;
    private readonly Lazy<IReadOnlySet<string>> _creatorIDs;
    private readonly Lazy<IReadOnlyDictionary<CreatorRoleType, IReadOnlySet<string>>> _creatorRoles;

    public string Name => _name.Value;

    public required Func<string> NameDelegate
    {
        init => _name = new Lazy<string>(value);
    }

    public IReadOnlySet<string> Names => _names.Value;

    public required Func<IReadOnlySet<string>> NamesDelegate
    {
        init => _names = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> AniDBIDs => _aniDbIds.Value;

    public required Func<IReadOnlySet<string>> AniDBIDsDelegate
    {
        init => _aniDbIds = new Lazy<IReadOnlySet<string>>(value);
    }

    public string SortingName => _sortingName.Value;

    public required Func<string> SortingNameDelegate
    {
        init => _sortingName = new Lazy<string>(value);
    }

    public int SeriesCount => _seriesCount.Value;

    public required Func<int> SeriesCountDelegate
    {
        init => _seriesCount = new Lazy<int>(value);
    }

    public int MissingEpisodes => _missingEpisodes.Value;

    public required Func<int> MissingEpisodesDelegate
    {
        init => _missingEpisodes = new Lazy<int>(value);
    }

    public int MissingEpisodesCollecting => _missingEpisodesCollecting.Value;

    public required Func<int> MissingEpisodesCollectingDelegate
    {
        init => _missingEpisodesCollecting = new Lazy<int>(value);
    }

    public int VideoFiles => _videoFiles.Value;

    public required Func<int> VideoFilesDelegate
    {
        init => _videoFiles = new Lazy<int>(value);
    }

    public IReadOnlySet<string> AnidbTagIDs => _anidbTagIDs.Value;

    public required Func<IReadOnlySet<string>> AnidbTagIDsDelegate
    {
        init => _anidbTagIDs = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> AnidbTags => _anidbTags.Value;

    public required Func<IReadOnlySet<string>> AnidbTagsDelegate
    {
        init => _anidbTags = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> CustomTagIDs => _customTagIDs.Value;

    public required Func<IReadOnlySet<string>> CustomTagIDsDelegate
    {
        init => _customTagIDs = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> CustomTags => _customTags.Value;

    public required Func<IReadOnlySet<string>> CustomTagsDelegate
    {
        init => _customTags = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<int> Years => _years.Value;

    public required Func<IReadOnlySet<int>> YearsDelegate
    {
        init => _years = new Lazy<IReadOnlySet<int>>(value);
    }

    public IReadOnlySet<(int year, AnimeSeason season)> Seasons => _seasons.Value;

    public required Func<IReadOnlySet<(int year, AnimeSeason season)>> SeasonsDelegate
    {
        init => _seasons = new Lazy<IReadOnlySet<(int year, AnimeSeason season)>>(value);
    }

    public IReadOnlySet<ImageEntityType> AvailableImageTypes => _availableImageTypes.Value;

    public required Func<IReadOnlySet<ImageEntityType>> AvailableImageTypesDelegate
    {
        init => _availableImageTypes = new Lazy<IReadOnlySet<ImageEntityType>>(value);
    }

    public IReadOnlySet<ImageEntityType> PreferredImageTypes => _preferredImageTypes.Value;

    public required Func<IReadOnlySet<ImageEntityType>> PreferredImageTypesDelegate
    {
        init => _preferredImageTypes = new Lazy<IReadOnlySet<ImageEntityType>>(value);
    }

    public IReadOnlySet<string> CharacterIDs => _characterIDs.Value;

    public required Func<IReadOnlySet<string>> CharacterIDsDelegate
    {
        init => _characterIDs = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlyDictionary<CharacterAppearanceType, IReadOnlySet<string>> CharacterAppearances => _characterAppearances.Value;

    public required Func<IReadOnlyDictionary<CharacterAppearanceType, IReadOnlySet<string>>> CharacterAppearancesDelegate
    {
        init => _characterAppearances = new Lazy<IReadOnlyDictionary<CharacterAppearanceType, IReadOnlySet<string>>>(value);
    }

    public IReadOnlySet<string> CreatorIDs => _creatorIDs.Value;

    public required Func<IReadOnlySet<string>> CreatorIDsDelegate
    {
        init => _creatorIDs = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlyDictionary<CreatorRoleType, IReadOnlySet<string>> CreatorRoles => _creatorRoles.Value;

    public required Func<IReadOnlyDictionary<CreatorRoleType, IReadOnlySet<string>>> CreatorRolesDelegate
    {
        init => _creatorRoles = new Lazy<IReadOnlyDictionary<CreatorRoleType, IReadOnlySet<string>>>(value);
    }

    public bool HasTmdbLink => _hasTmdbLink.Value;

    public required Func<bool> HasTmdbLinkDelegate
    {
        init => _hasTmdbLink = new Lazy<bool>(value);
    }

    public bool HasTmdbAutoLinkingDisabled => _hasTmdbAutoLinkingDisabled.Value;

    public required Func<bool> HasTmdbAutoLinkingDisabledDelegate
    {
        init => _hasTmdbAutoLinkingDisabled = new Lazy<bool>(value);
    }

    public int AutomaticTmdbEpisodeLinks => _automaticTmdbEpisodeLinks.Value;

    public required Func<int> AutomaticTmdbEpisodeLinksDelegate
    {
        init => _automaticTmdbEpisodeLinks = new Lazy<int>(value);
    }

    public int UserVerifiedTmdbEpisodeLinks => _userVerifiedTmdbEpisodeLinks.Value;

    public required Func<int> UserVerifiedTmdbEpisodeLinksDelegate
    {
        init => _userVerifiedTmdbEpisodeLinks = new Lazy<int>(value);
    }

    public bool HasTraktLink => _hasTraktLink.Value;

    public required Func<bool> HasTraktLinkDelegate
    {
        init => _hasTraktLink = new Lazy<bool>(value);
    }

    public bool HasTraktAutoLinkingDisabled => _hasTraktAutoLinkingDisabled.Value;

    public required Func<bool> HasTraktAutoLinkingDisabledDelegate
    {
        init => _hasTraktAutoLinkingDisabled = new Lazy<bool>(value);
    }

    public bool IsFinished => _isFinished.Value;

    public required Func<bool> IsFinishedDelegate
    {
        init => _isFinished = new Lazy<bool>(value);
    }

    public DateTime? AirDate => _airDate.Value;

    public required Func<DateTime?> AirDateDelegate
    {
        init => _airDate = new Lazy<DateTime?>(value);
    }

    public DateTime? LastAirDate => _lastAirDate.Value;

    public required Func<DateTime?> LastAirDateDelegate
    {
        init => _lastAirDate = new Lazy<DateTime?>(value);
    }

    public DateTime AddedDate => _addedDate.Value;

    public required Func<DateTime> AddedDateDelegate
    {
        init => _addedDate = new Lazy<DateTime>(value);
    }

    public DateTime LastAddedDate => _lastAddedDate.Value;

    public required Func<DateTime> LastAddedDateDelegate
    {
        init => _lastAddedDate = new Lazy<DateTime>(value);
    }

    public int EpisodeCount => _episodeCount.Value;

    public required Func<int> EpisodeCountDelegate
    {
        init => _episodeCount = new Lazy<int>(value);
    }

    public int TotalEpisodeCount => _totalEpisodeCount.Value;

    public required Func<int> TotalEpisodeCountDelegate
    {
        init => _totalEpisodeCount = new Lazy<int>(value);
    }

    public decimal LowestAniDBRating => _lowestAniDBRating.Value;

    public required Func<decimal> LowestAniDBRatingDelegate
    {
        init => _lowestAniDBRating = new Lazy<decimal>(value);
    }

    public decimal HighestAniDBRating => _highestAniDBRating.Value;

    public required Func<decimal> HighestAniDBRatingDelegate
    {
        init => _highestAniDBRating = new Lazy<decimal>(value);
    }

    public decimal AverageAniDBRating => _averageAniDBRating.Value;

    public required Func<decimal> AverageAniDBRatingDelegate
    {
        init => _averageAniDBRating = new Lazy<decimal>(value);
    }

    public IReadOnlySet<string> VideoSources => _videoSources.Value;

    public required Func<IReadOnlySet<string>> VideoSourcesDelegate
    {
        init => _videoSources = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> SharedVideoSources => _sharedVideoSources.Value;

    public required Func<IReadOnlySet<string>> SharedVideoSourcesDelegate
    {
        init => _sharedVideoSources = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> AnimeTypes => _animeTypes.Value;

    public required Func<IReadOnlySet<string>> AnimeTypesDelegate
    {
        init => _animeTypes = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> AudioLanguages => _audioLanguages.Value;

    public required Func<IReadOnlySet<string>> AudioLanguagesDelegate
    {
        init => _audioLanguages = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> SharedAudioLanguages => _sharedAudioLanguages.Value;

    public required Func<IReadOnlySet<string>> SharedAudioLanguagesDelegate
    {
        init => _sharedAudioLanguages = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> SubtitleLanguages => _subtitleLanguages.Value;

    public required Func<IReadOnlySet<string>> SubtitleLanguagesDelegate
    {
        init => _subtitleLanguages = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> SharedSubtitleLanguages => _sharedSubtitleLanguages.Value;

    public required Func<IReadOnlySet<string>> SharedSubtitleLanguagesDelegate
    {
        init => _sharedSubtitleLanguages = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> Resolutions => _resolutions.Value;
    public required Func<IReadOnlySet<string>> ResolutionsDelegate
    {
        init
        {
            _resolutions = new Lazy<IReadOnlySet<string>>(value);
        }
    }

    public IReadOnlySet<string> ManagedFolderIDs => _managedFolderIDs.Value;

    public required Func<IReadOnlySet<string>> ManagedFolderIDsDelegate
    {
        init => _managedFolderIDs = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> ManagedFolderNames => _managedFolderNames.Value;

    public required Func<IReadOnlySet<string>> ManagedFolderNamesDelegate
    {
        init => _managedFolderNames = new Lazy<IReadOnlySet<string>>(value);
    }

    public IReadOnlySet<string> FilePaths => _filePaths.Value;

    public required Func<IReadOnlySet<string>> FilePathsDelegate
    {
        init => _filePaths = new Lazy<IReadOnlySet<string>>(value);
    }
}
