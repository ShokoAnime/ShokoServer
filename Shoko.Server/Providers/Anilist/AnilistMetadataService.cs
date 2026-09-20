using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Anilist;
using Shoko.Abstractions.Metadata.Anilist.Enums;
using Shoko.Abstractions.Metadata.Anilist.Services;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.Anilist;
using Shoko.Server.Scheduling.Jobs.Anilist;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Providers.Anilist;

/// <summary>
/// Service for managing AniList anime metadata. Mirrors
/// <see cref="TmdbMetadataService"/> for the parts AniList has.
/// </summary>
public class AnilistMetadataService : IAnilistMetadataService
{
    private readonly ILogger<AnilistMetadataService> _logger;

    private readonly ISettingsProvider _settingsProvider;

    private readonly IQueueScheduler _scheduler;

    private readonly AnilistApiClient _apiClient;

    private readonly AnilistRateLimiter _rateLimiter;

    private readonly AnilistImageService _imageService;

    private readonly AnilistLinkingService _linkingService;

    private readonly IAiringScheduleService _airingScheduleService;

    private readonly ConfigurationProvider<AiringScheduleServiceSettings> _airingScheduleSettings;

    private readonly AnimeSeriesRepository _animeSeries;

    private readonly Anilist_AnimeRepository _anilistAnime;

    private readonly Anilist_EpisodeRepository _anilistEpisodes;

    private readonly Anilist_TagRepository _anilistTags;

    private readonly Anilist_Anime_TagRepository _anilistAnimeTags;

    private readonly Anilist_StudioRepository _anilistStudios;

    private readonly Anilist_Anime_StudioRepository _anilistAnimeStudios;

    private readonly Anilist_Anime_ExternalLinkRepository _anilistAnimeExternalLinks;

    private readonly Anilist_CharacterRepository _anilistCharacters;

    private readonly Anilist_CreatorRepository _anilistCreators;

    private readonly Anilist_Anime_CharacterRepository _anilistAnimeCharacters;

    private readonly Anilist_Anime_Character_CreatorRepository _anilistAnimeCharacterCreators;

    private readonly Anilist_Anime_StaffRepository _anilistAnimeStaff;

    private readonly Anilist_Anime_RelationRepository _anilistAnimeRelations;

    private readonly Anilist_Anime_SuggestionRepository _anilistAnimeSuggestions;

    private readonly CrossRef_AniDB_Anilist_AnimeRepository _xrefAnidbAnilistAnime;

    private readonly CrossRef_AniDB_Anilist_EpisodeRepository _xrefAnidbAnilistEpisodes;

    private readonly KeyedEntityLockHelper _entityLock;

    /// <summary>
    /// Cached tags dictionary.
    /// </summary>
    private IReadOnlyDictionary<int, string>? _tags;

    public AnilistMetadataService(
        ILogger<AnilistMetadataService> logger,
        ISettingsProvider settingsProvider,
        IQueueScheduler scheduler,
        AnilistApiClient apiClient,
        AnilistRateLimiter rateLimiter,
        AnilistImageService imageService,
        AnilistLinkingService linkingService,
        IAiringScheduleService airingScheduleService,
        ConfigurationProvider<AiringScheduleServiceSettings> airingScheduleSettings,
        AnimeSeriesRepository animeSeries,
        Anilist_AnimeRepository anilistAnime,
        Anilist_EpisodeRepository anilistEpisodes,
        Anilist_TagRepository anilistTags,
        Anilist_Anime_TagRepository anilistAnimeTags,
        Anilist_StudioRepository anilistStudios,
        Anilist_Anime_StudioRepository anilistAnimeStudios,
        Anilist_Anime_ExternalLinkRepository anilistAnimeExternalLinks,
        Anilist_CharacterRepository anilistCharacters,
        Anilist_CreatorRepository anilistCreators,
        Anilist_Anime_CharacterRepository anilistAnimeCharacters,
        Anilist_Anime_Character_CreatorRepository anilistAnimeCharacterCreators,
        Anilist_Anime_StaffRepository anilistAnimeStaff,
        Anilist_Anime_RelationRepository anilistAnimeRelations,
        Anilist_Anime_SuggestionRepository anilistAnimeSuggestions,
        CrossRef_AniDB_Anilist_AnimeRepository xrefAnidbAnilistAnime,
        CrossRef_AniDB_Anilist_EpisodeRepository xrefAnidbAnilistEpisodes
    )
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _scheduler = scheduler;
        _apiClient = apiClient;
        _rateLimiter = rateLimiter;
        _imageService = imageService;
        _linkingService = linkingService;
        _airingScheduleService = airingScheduleService;
        _airingScheduleSettings = airingScheduleSettings;
        _animeSeries = animeSeries;
        _anilistAnime = anilistAnime;
        _anilistEpisodes = anilistEpisodes;
        _anilistTags = anilistTags;
        _anilistAnimeTags = anilistAnimeTags;
        _anilistStudios = anilistStudios;
        _anilistAnimeStudios = anilistAnimeStudios;
        _anilistAnimeExternalLinks = anilistAnimeExternalLinks;
        _anilistCharacters = anilistCharacters;
        _anilistCreators = anilistCreators;
        _anilistAnimeCharacters = anilistAnimeCharacters;
        _anilistAnimeCharacterCreators = anilistAnimeCharacterCreators;
        _anilistAnimeStaff = anilistAnimeStaff;
        _anilistAnimeRelations = anilistAnimeRelations;
        _anilistAnimeSuggestions = anilistAnimeSuggestions;
        _xrefAnidbAnilistAnime = xrefAnidbAnilistAnime;
        _xrefAnidbAnilistEpisodes = xrefAnidbAnilistEpisodes;
        _entityLock = new(logger);
    }

    #region Tags

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<int, string>> GetTags()
    {
        if (_tags is not null)
            return Task.FromResult(_tags);

        _tags = _anilistTags.GetAll().ToDictionary(t => t.AnilistTagID, t => t.Name);
        return Task.FromResult(_tags);
    }

    #endregion

    #region Anime - Update

    /// <inheritdoc/>
    public async Task UpdateAllAnime(bool force = false, bool downloadImages = false)
    {
        var allXRefs = _xrefAnidbAnilistAnime.GetAll();
        _logger.LogInformation("Scheduling {Count} AniList anime to be updated.", allXRefs.Count);
        foreach (var xref in allXRefs)
        {
            if (xref.AnilistAnimeID is 0)
                continue;

            if (xref.AnimeSeries is null)
                continue;

            await _scheduler.StartJob<UpdateAnilistAnimeJob>(c =>
            {
                c.AnilistAnimeID = xref.AnilistAnimeID;
                c.ForceRefresh = force;
                c.DownloadImages = downloadImages;
            }).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task ScheduleUpdateOfAnime(AnilistAnimeUpdateOptions options)
    {
        if (options.AnimeId is 0)
            return;

        // Schedule the anime info to be downloaded or updated.
        await _scheduler.RunAfterCurrent<UpdateAnilistAnimeJob>(c =>
        {
            c.AnilistAnimeID = options.AnimeId;
            c.ForceRefresh = options.ForceRefresh;
            c.QuickRefresh = options.QuickRefresh;
            c.DownloadImages = options.DownloadImages;
            c.DownloadCharactersAndStaff = options.DownloadCharactersAndStaff;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether the anime is currently being updated.
    /// </summary>
    public bool IsAnimeUpdating(int anilistAnimeId)
        => _entityLock.IsEntityLocked(DataEntityType.Anime, anilistAnimeId, "metadata");

    /// <summary>
    /// Wait for a running update of the anime to finish. Returns whether one was running.
    /// </summary>
    public bool WaitForAnimeUpdate(int anilistAnimeId)
        => _entityLock.WaitIfEntityLocked(DataEntityType.Anime, anilistAnimeId, "metadata");

    /// <inheritdoc/>
    public async Task<bool> UpdateAnime(AnilistAnimeUpdateOptions options)
    {
        var anilistAnimeId = options.AnimeId;
        if (anilistAnimeId is 0)
            return false;

        using (await _entityLock.GetLockForEntityAsync(DataEntityType.Anime, anilistAnimeId, "metadata", "Update").ConfigureAwait(false))
        {
            var settings = _settingsProvider.GetSettings();
            var anime = _anilistAnime.GetByAnilistAnimeID(anilistAnimeId);
            var isNew = anime is null;
            var xrefs = _xrefAnidbAnilistAnime.GetByAnilistAnimeID(anilistAnimeId);
            if (anime is null)
            {
                anime = new Anilist_Anime(anilistAnimeId);
            }
            // A quick-fetched anime keeps `CreatedAt == LastUpdatedAt`, so it never counts as recently updated.
            else if (!options.ForceRefresh && anime.CreatedAt != anime.LastUpdatedAt && anime.LastUpdatedAt > DateTime.Now.AddHours(-1))
            {
                _logger.LogInformation("Skipping update of AniList anime {AnimeId} as it was last updated {LastUpdatedAt}", anilistAnimeId, anime.LastUpdatedAt);

                // Do the auto-matching if we're not doing a quick refresh.
                if (!options.QuickRefresh)
                    foreach (var xref in xrefs)
                        _linkingService.MatchAnidbToAnilistEpisodes(xref.AnidbAnimeID, xref.AnilistAnimeID, true, true);

                return false;
            }

            _logger.LogInformation("Updating AniList anime {AnimeId}", anilistAnimeId);

            // Fetch anime data from API
            var mediaNode = await _apiClient.GetAnimeByIdAsync(anilistAnimeId).ConfigureAwait(false);
            if (mediaNode is null)
            {
                _logger.LogWarning("AniList has no anime with ID {AnimeId}", anilistAnimeId);
                return false;
            }

            // Update anime properties from API response
            var updated = PopulateAnimeFromJson(anime, mediaNode);
            if (updated || isNew)
            {
                // A quick refresh only fetches what the linking UI needs, so leave the record
                // looking newly added and let the full refresh after linking stamp it.
                if (!options.QuickRefresh)
                    anime.LastUpdatedAt = DateTime.Now;
                _anilistAnime.Save(anime);
            }

            await UpdateAnimeEpisodes(anime, mediaNode).ConfigureAwait(false);
            UpdateAnimeTags(anilistAnimeId, mediaNode);
            UpdateAnimeStudios(anilistAnimeId, mediaNode);
            UpdateAnimeRelations(anilistAnimeId, mediaNode);

            await UpdateAnimeSuggestions(anilistAnimeId, mediaNode).ConfigureAwait(false);
            UpdateAnimeExternalLinks(anilistAnimeId, mediaNode);
            if (!options.QuickRefresh)
            {
                if (options.DownloadCharactersAndStaff ?? settings.Anilist.AutoDownloadCharacters)
                    await UpdateAnimeCharacters(anilistAnimeId, mediaNode).ConfigureAwait(false);
                if (options.DownloadCharactersAndStaff ?? settings.Anilist.AutoDownloadStaff)
                    await UpdateAnimeStaff(anilistAnimeId, mediaNode).ConfigureAwait(false);
            }

            // Clear tag cache since tags may have been updated
            _tags = null;

            // Match the episodes for every linked series now that the episodes exist.
            foreach (var xref in xrefs)
            {
                // Don't do the auto-matching if we're just doing a quick refresh.
                if (!options.QuickRefresh)
                    _linkingService.MatchAnidbToAnilistEpisodes(xref.AnidbAnimeID, xref.AnilistAnimeID, true, true);

                if (updated && xref.AnimeSeries is { } series)
                {
                    series.ResetPreferredTitle();
                    series.ResetAnimeTitles();
                    series.ResetPreferredOverview();
                }
            }

            if (options.DownloadImages && !options.QuickRefresh)
                await ScheduleDownloadAllAnimeImages(anilistAnimeId, false).ConfigureAwait(false);

            if (isNew || updated)
                _logger.LogInformation("AniList anime {AnimeId} ({Title}) {Action}", anilistAnimeId, anime.PreferredTitle, isNew ? "added" : "updated");

            return updated;
        }
    }

    private static bool PopulateAnimeFromJson(Anilist_Anime anime, JsonNode media)
    {
        var updated = false;

        // Titles
        var title = media["title"];
        updated |= anime.UpdateProperty(anime.EnglishTitle, title?["english"]?.GetValue<string>() ?? string.Empty, v => anime.EnglishTitle = v);
        updated |= anime.UpdateProperty(anime.MainTitle, title?["romaji"]?.GetValue<string>() ?? string.Empty, v => anime.MainTitle = v);
        updated |= anime.UpdateProperty(anime.NativeTitle, title?["native"]?.GetValue<string>() ?? string.Empty, v => anime.NativeTitle = v);
        updated |= anime.UpdateProperty(anime.Synonyms, ParseStringList(media["synonyms"]), v => anime.Synonyms = v, StringListEquals);

        // Description
        updated |= anime.UpdateProperty(anime.EnglishOverview, AnilistUtility.StripHtml(media["description"]?.GetValue<string>()), v => anime.EnglishOverview = v);

        // Classification
        updated |= anime.UpdateProperty(anime.OriginalLanguageCode, AnilistUtility.ParseOriginalLanguage(media["countryOfOrigin"]?.GetValue<string>()), v => anime.OriginalLanguageCode = v);
        updated |= anime.UpdateProperty(anime.Type, AnilistUtility.ParseFormat(media["format"]?.GetValue<string>()), v => anime.Type = v);
        updated |= anime.UpdateProperty(anime.ReleasingStatus, AnilistUtility.ParseMediaStatus(media["status"]?.GetValue<string>()), v => anime.ReleasingStatus = v);
        updated |= anime.UpdateProperty(anime.MediaSource, AnilistUtility.ParseMediaSource(media["source"]?.GetValue<string>()), v => anime.MediaSource = v);
        updated |= anime.UpdateProperty(anime.Season, AnilistUtility.ParseSeason(media["season"]?.GetValue<string>()), v => anime.Season = v);
        updated |= anime.UpdateProperty(anime.SeasonYear, media["seasonYear"]?.GetValue<int?>(), v => anime.SeasonYear = v);

        // Images
        updated |= anime.UpdateProperty(anime.CoverImagePath, AnilistImageService.ToResourceID(media["coverImage"]?["extraLarge"]?.GetValue<string>()) ?? string.Empty, v => anime.CoverImagePath = v);
        updated |= anime.UpdateProperty(anime.BannerImagePath, AnilistImageService.ToResourceID(media["bannerImage"]?.GetValue<string>()) ?? string.Empty, v => anime.BannerImagePath = v);
        updated |= anime.UpdateProperty(anime.Color, media["coverImage"]?["color"]?.GetValue<string>() ?? string.Empty, v => anime.Color = v);

        // Counts and ratings
        updated |= anime.UpdateProperty(anime.EpisodeCount, media["episodes"]?.GetValue<int?>() ?? 0, v => anime.EpisodeCount = v);
        updated |= anime.UpdateProperty(anime.EpisodeDuration, media["duration"]?.GetValue<int?>(), v => anime.EpisodeDuration = v);
        updated |= anime.UpdateProperty(anime.UserRating, media["averageScore"]?.GetValue<int?>() ?? 0, v => anime.UserRating = v, (a, b) => Math.Abs(a - b) < 0.01);
        updated |= anime.UpdateProperty(anime.MeanScore, media["meanScore"]?.GetValue<int?>() ?? 0, v => anime.MeanScore = v, (a, b) => Math.Abs(a - b) < 0.01);
        updated |= anime.UpdateProperty(anime.FavoriteCount, media["favourites"]?.GetValue<int?>() ?? 0, v => anime.FavoriteCount = v);
        updated |= anime.UpdateProperty(anime.Popularity, media["popularity"]?.GetValue<int?>() ?? 0, v => anime.Popularity = v);
        updated |= anime.UpdateProperty(anime.IsRestricted, media["isAdult"]?.GetValue<bool?>() ?? false, v => anime.IsRestricted = v);
        updated |= anime.UpdateProperty(anime.IsLicensed, media["isLicensed"]?.GetValue<bool?>() ?? true, v => anime.IsLicensed = v);
        updated |= anime.UpdateProperty(anime.Genres, ParseStringList(media["genres"]), v => anime.Genres = v, StringListEquals);
        updated |= anime.UpdateProperty(anime.MalID, media["idMal"]?.GetValue<int?>(), v => anime.MalID = v);
        updated |= anime.UpdateProperty(anime.TrailerSite, media["trailer"]?["site"]?.GetValue<string>(), v => anime.TrailerSite = v);
        updated |= anime.UpdateProperty(anime.TrailerID, media["trailer"]?["id"]?.GetValue<string>(), v => anime.TrailerID = v);

        // Dates
        updated |= anime.UpdateProperty(anime.FirstAiredAt, ParsePartialDate(media["startDate"]), v => anime.FirstAiredAt = v);
        updated |= anime.UpdateProperty(anime.LastAiredAt, ParsePartialDate(media["endDate"]), v => anime.LastAiredAt = v);

        return updated;
    }

    private static List<string> ParseStringList(JsonNode? node)
        => node is JsonArray array
            ? array.Select(item => item?.GetValue<string>()).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!.Trim()).Distinct().ToList()
            : [];

    private static bool StringListEquals(List<string> a, List<string> b)
        => a.Count == b.Count && a.SequenceEqual(b, StringComparer.Ordinal);

    private static PartialDateOnly? ParsePartialDate(JsonNode? node)
    {
        if (node is null)
            return null;

        var year = node["year"]?.GetValue<int?>();
        var month = node["month"]?.GetValue<int?>();
        var day = node["day"]?.GetValue<int?>();
        return year.HasValue ? new(year.Value, month, day) : null;
    }

    private static PersonGender ParseGender(string? gender) => gender?.Trim().ToLowerInvariant() switch
    {
        "male" => PersonGender.Male,
        "female" => PersonGender.Female,
        "non-binary" or "nonbinary" => PersonGender.NonBinary,
        _ => PersonGender.Unknown,
    };

    /// <summary>
    /// Synthesize the episode rows for the anime. AniList has no episode
    /// entity, so we create one row per episode number up to the larger of the
    /// reported episode count and the highest scheduled episode, and attach
    /// the airing schedule entry when one exists.
    /// </summary>
    private async Task UpdateAnimeEpisodes(Anilist_Anime anime, JsonNode media)
    {
        var anilistAnimeId = anime.AnilistAnimeID;
        if (!AnilistUtility.CanPackEpisodeID(anilistAnimeId, 0))
        {
            _logger.LogWarning("AniList anime {AnimeId} is above the packable anime ID limit ({Limit}). Skipping episode synthesis.", anilistAnimeId, AnilistUtility.MaxAnimeID);
            return;
        }

        // Collect the full airing schedule, following the pagination.
        var schedule = new Dictionary<int, (int ScheduleId, DateTime? AiredAt)>();
        var airingSchedule = media["airingSchedule"];
        while (airingSchedule is not null)
        {
            if (airingSchedule["nodes"] is JsonArray nodes)
            {
                foreach (var node in nodes)
                {
                    if (node is null)
                        continue;

                    var scheduleId = node["id"]?.GetValue<int>() ?? 0;
                    var episodeNumber = node["episode"]?.GetValue<int>() ?? 0;
                    var airingAt = node["airingAt"]?.GetValue<long?>() ?? 0;
                    if (scheduleId is 0 || episodeNumber is 0)
                        continue;

                    var airedAt = airingAt > 0 ? DateTimeOffset.FromUnixTimeSeconds(airingAt).UtcDateTime : (DateTime?)null;
                    schedule[episodeNumber] = (scheduleId, airedAt);
                }
            }

            var pageInfo = airingSchedule["pageInfo"];
            if (!(pageInfo?["hasNextPage"]?.GetValue<bool>() ?? false))
                break;

            airingSchedule = await _apiClient.GetAiringSchedulePageAsync(anilistAnimeId, (pageInfo?["currentPage"]?.GetValue<int>() ?? 1) + 1).ConfigureAwait(false);
        }

        var episodeCount = Math.Max(anime.EpisodeCount, schedule.Count > 0 ? schedule.Keys.Max() : 0);
        if (episodeCount > AnilistUtility.MaxEpisodeNumber)
        {
            _logger.LogWarning("AniList anime {AnimeId} reports {Count} episodes, above the packable episode limit ({Limit}). Only the first {Limit} will be synthesized.", anilistAnimeId, episodeCount, AnilistUtility.MaxEpisodeNumber, AnilistUtility.MaxEpisodeNumber);
            episodeCount = AnilistUtility.MaxEpisodeNumber;
        }

        var existingEpisodes = _anilistEpisodes.GetByAnilistAnimeID(anilistAnimeId).ToDictionary(e => e.EpisodeNumber);
        var toSave = new List<Anilist_Episode>();
        var now = DateTime.Now;
        for (var episodeNumber = 1; episodeNumber <= episodeCount; episodeNumber++)
        {
            var scheduleId = schedule.TryGetValue(episodeNumber, out var entry) ? entry.ScheduleId : (int?)null;
            var airedAt = scheduleId.HasValue ? entry.AiredAt : null;
            if (!existingEpisodes.Remove(episodeNumber, out var episode))
            {
                toSave.Add(new Anilist_Episode(anilistAnimeId, episodeNumber)
                {
                    AnilistScheduleEpisodeID = scheduleId,
                    RuntimeMinutes = anime.EpisodeDuration,
                    AiredAt = airedAt,
                });
                continue;
            }

            var updated = false;
            updated |= episode.UpdateProperty(episode.AnilistScheduleEpisodeID, scheduleId, v => episode.AnilistScheduleEpisodeID = v);
            updated |= episode.UpdateProperty(episode.RuntimeMinutes, anime.EpisodeDuration, v => episode.RuntimeMinutes = v);
            updated |= episode.UpdateProperty(episode.AiredAt, airedAt, v => episode.AiredAt = v);
            if (updated)
            {
                episode.LastUpdatedAt = now;
                toSave.Add(episode);
            }
        }

        // Anything left over is above the current episode count and no longer exists.
        var toRemove = existingEpisodes.Values.ToList();
        if (toSave.Count > 0)
            _anilistEpisodes.Save(toSave);
        if (toRemove.Count > 0)
        {
            var xrefsToRemove = toRemove
                .SelectMany(episode => _xrefAnidbAnilistEpisodes.GetByAnilistEpisodeID(episode.AnilistEpisodeID))
                .ToList();
            if (xrefsToRemove.Count > 0)
                _xrefAnidbAnilistEpisodes.Delete(xrefsToRemove);
            _anilistEpisodes.Delete(toRemove);
        }

        if (toSave.Count > 0 || toRemove.Count > 0)
            _logger.LogDebug("Synthesized episodes for AniList anime {AnimeId}: {Saved} saved, {Removed} removed, {Scheduled} with a schedule entry.", anilistAnimeId, toSave.Count, toRemove.Count, schedule.Count);

        UpdateAiringSchedule(anime, schedule, episodeCount);
    }

    private void UpdateAnimeTags(int anilistAnimeId, JsonNode media)
    {
        var existingAnimeTags = _anilistAnimeTags.GetByAnilistAnimeID(anilistAnimeId).ToDictionary(t => t.AnilistTagID);
        if (media["tags"] is JsonArray tagsArray)
        {
            foreach (var tagNode in tagsArray)
            {
                if (tagNode is null)
                    continue;

                var tagId = tagNode["id"]?.GetValue<int>() ?? 0;
                if (tagId is 0)
                    continue;

                var name = tagNode["name"]?.GetValue<string>() ?? string.Empty;
                var category = tagNode["category"]?.GetValue<string>() ?? string.Empty;
                var description = tagNode["description"]?.GetValue<string>() ?? string.Empty;
                var isAdult = tagNode["isAdult"]?.GetValue<bool>() ?? false;
                var isGeneralSpoiler = tagNode["isGeneralSpoiler"]?.GetValue<bool>() ?? false;
                var isMediaSpoiler = tagNode["isMediaSpoiler"]?.GetValue<bool>() ?? false;
                var rank = tagNode["rank"]?.GetValue<int?>() ?? 0;

                var tag = _anilistTags.GetByAnilistTagID(tagId) ?? new Anilist_Tag(tagId);
                var tagUpdated = tag.Anilist_TagID is 0;
                tagUpdated |= tag.UpdateProperty(tag.Name, name, v => tag.Name = v);
                tagUpdated |= tag.UpdateProperty(tag.Category, category, v => tag.Category = v);
                tagUpdated |= tag.UpdateProperty(tag.Description, description, v => tag.Description = v);
                tagUpdated |= tag.UpdateProperty(tag.IsRestricted, isAdult, v => tag.IsRestricted = v);
                tagUpdated |= tag.UpdateProperty(tag.IsSpoiler, isGeneralSpoiler, v => tag.IsSpoiler = v);
                if (tagUpdated)
                {
                    tag.LastUpdatedAt = DateTime.Now;
                    _anilistTags.Save(tag);
                }

                if (!existingAnimeTags.Remove(tagId, out var animeTag))
                {
                    _anilistAnimeTags.Save(new Anilist_Anime_Tag(anilistAnimeId, tagId, rank, isMediaSpoiler));
                }
                else if (animeTag.Weight != rank || animeTag.IsLocalSpoiler != isMediaSpoiler)
                {
                    animeTag.Weight = rank;
                    animeTag.IsLocalSpoiler = isMediaSpoiler;
                    _anilistAnimeTags.Save(animeTag);
                }
            }
        }

        // Anything left over was removed from the anime upstream.
        if (existingAnimeTags.Count > 0)
            _anilistAnimeTags.Delete(existingAnimeTags.Values.ToList());
    }

    private void UpdateAnimeStudios(int anilistAnimeId, JsonNode media)
    {
        var settings = _settingsProvider.GetSettings();
        var existingAnimeStudios = _anilistAnimeStudios.GetByAnilistAnimeID(anilistAnimeId).ToDictionary(s => s.AnilistStudioID);
        if (settings.Anilist.AutoDownloadStudios && media["studios"]?["edges"] is JsonArray edges)
        {
            foreach (var edge in edges)
            {
                if (edge?["node"] is not { } studioNode)
                    continue;

                var studioId = studioNode["id"]?.GetValue<int>() ?? 0;
                if (studioId is 0)
                    continue;

                var isMain = edge["isMain"]?.GetValue<bool>() ?? false;
                var studio = _anilistStudios.GetByAnilistStudioID(studioId) ?? new Anilist_Studio(studioId);
                var studioUpdated = studio.Anilist_StudioID is 0;
                studioUpdated |= studio.UpdateProperty(studio.Name, studioNode["name"]?.GetValue<string>() ?? string.Empty, v => studio.Name = v);
                studioUpdated |= studio.UpdateProperty(studio.IsAnimationStudio, studioNode["isAnimationStudio"]?.GetValue<bool>() ?? false, v => studio.IsAnimationStudio = v);
                studioUpdated |= studio.UpdateProperty(studio.FavoriteCount, studioNode["favourites"]?.GetValue<int?>() ?? 0, v => studio.FavoriteCount = v);
                if (studioUpdated)
                {
                    studio.LastUpdatedAt = DateTime.Now;
                    _anilistStudios.Save(studio);
                }

                if (!existingAnimeStudios.Remove(studioId, out var animeStudio))
                {
                    _anilistAnimeStudios.Save(new Anilist_Anime_Studio(anilistAnimeId, studioId, isMain));
                }
                else if (animeStudio.IsMainStudio != isMain)
                {
                    animeStudio.IsMainStudio = isMain;
                    _anilistAnimeStudios.Save(animeStudio);
                }
            }
        }

        // Anything left over was removed from the anime upstream, or studios were turned off.
        if (existingAnimeStudios.Count > 0)
            _anilistAnimeStudios.Delete(existingAnimeStudios.Values.ToList());
    }

    private void UpdateAnimeExternalLinks(int anilistAnimeId, JsonNode media)
    {
        var existingLinks = _anilistAnimeExternalLinks.GetByAnilistAnimeID(anilistAnimeId).ToDictionary(l => l.AnilistLinkID);
        if (media["externalLinks"] is JsonArray links)
        {
            foreach (var node in links)
            {
                if (node is null)
                    continue;

                var linkId = node["id"]?.GetValue<int>() ?? 0;
                var url = node["url"]?.GetValue<string>();
                if (linkId is 0 || string.IsNullOrWhiteSpace(url))
                    continue;

                var languageCode = node["language"]?.GetValue<string>() is { Length: > 0 } language
                    && language.GetTitleLanguage() is not (TitleLanguage.None or TitleLanguage.Unknown) and var titleLanguage
                    ? titleLanguage.GetString()
                    : null;
                var isNew = !existingLinks.Remove(linkId, out var link);
                link ??= new Anilist_Anime_ExternalLink(anilistAnimeId, linkId);
                var linkUpdated = isNew;
                linkUpdated |= link.UpdateProperty(link.Url, url, v => link.Url = v);
                linkUpdated |= link.UpdateProperty(link.Site, node["site"]?.GetValue<string>() ?? string.Empty, v => link.Site = v);
                linkUpdated |= link.UpdateProperty(link.AnilistSiteID, node["siteId"]?.GetValue<int?>(), v => link.AnilistSiteID = v);
                linkUpdated |= link.UpdateProperty(link.LinkType, node["type"]?.GetValue<string>() ?? string.Empty, v => link.LinkType = v);
                linkUpdated |= link.UpdateProperty(link.LanguageCode, languageCode, v => link.LanguageCode = v);
                if (linkUpdated)
                    _anilistAnimeExternalLinks.Save(link);
            }
        }

        // Anything left over was removed from the anime upstream.
        if (existingLinks.Count > 0)
            _anilistAnimeExternalLinks.Delete(existingLinks.Values.ToList());
    }

    private void UpdateAnimeRelations(int anilistAnimeId, JsonNode media)
    {
        var existingRelations = _anilistAnimeRelations.GetByAnilistAnimeID(anilistAnimeId).ToDictionary(r => (r.RelatedAnilistID, r.RelationType));
        if (media["relations"]?["edges"] is JsonArray edges)
        {
            foreach (var edge in edges)
            {
                if (edge?["node"] is not { } node)
                    continue;

                var relatedId = node["id"]?.GetValue<int>() ?? 0;
                var relationType = edge["relationType"]?.GetValue<string>() ?? string.Empty;
                if (relatedId is 0 || string.IsNullOrEmpty(relationType))
                    continue;

                var relatedIsAnime = string.Equals(node["type"]?.GetValue<string>(), "ANIME", StringComparison.OrdinalIgnoreCase);
                if (existingRelations.Remove((relatedId, relationType), out var relation))
                {
                    if (relation.RelatedIsAnime != relatedIsAnime)
                    {
                        relation.RelatedIsAnime = relatedIsAnime;
                        _anilistAnimeRelations.Save(relation);
                    }
                    continue;
                }

                _anilistAnimeRelations.Save(new Anilist_Anime_Relation(anilistAnimeId, relatedId, relatedIsAnime, relationType));
            }
        }

        if (existingRelations.Count > 0)
            _anilistAnimeRelations.Delete(existingRelations.Values.ToList());
    }

    /// <summary>
    ///   The lowest rating on a page that is worth asking for the next one
    ///   under <see cref="AnilistRecommendationDepth.WhileWellRated"/>.
    ///   Recommendations come back best first, so once a whole page ends this
    ///   weak the rest is noise, and a further request is traffic spent on
    ///   entries nothing would show.
    /// </summary>
    private const int MinimumRatingToKeepPaging = 10;

    private async Task UpdateAnimeSuggestions(int anilistAnimeId, JsonNode media)
    {
        var depth = _settingsProvider.GetSettings().Anilist.RecommendationDepth;
        var existing = _anilistAnimeSuggestions.GetByAnilistAnimeID(anilistAnimeId).ToDictionary(s => s.SuggestedAnilistAnimeID);
        var ordering = 0;
        var page = media["recommendations"];
        while (page is not null)
        {
            var lowestRating = int.MaxValue;
            if (page["nodes"] is JsonArray nodes)
            {
                foreach (var entry in nodes)
                {
                    // A recommendation whose target was deleted upstream comes back null.
                    if (entry?["mediaRecommendation"] is not { } node)
                        continue;

                    var rating = entry["rating"]?.GetValue<int>() ?? 0;
                    if (rating < lowestRating)
                        lowestRating = rating;

                    var suggestedId = node["id"]?.GetValue<int>() ?? 0;
                    if (suggestedId is 0 || suggestedId == anilistAnimeId)
                        continue;

                    // AniList recommends manga and novels from an anime as well, and
                    // those are not anime we could ever hold.
                    if (!string.Equals(node["type"]?.GetValue<string>(), "ANIME", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var order = ordering++;
                    if (existing.Remove(suggestedId, out var suggestion))
                    {
                        if (suggestion.Rating == rating && suggestion.Ordering == order)
                            continue;

                        suggestion.Rating = rating;
                        suggestion.Ordering = order;
                        _anilistAnimeSuggestions.Save(suggestion);
                        continue;
                    }

                    _anilistAnimeSuggestions.Save(new Anilist_Anime_Suggestion(anilistAnimeId, suggestedId, rating, order));
                }
            }

            var pageInfo = page["pageInfo"];
            if (pageInfo?["hasNextPage"]?.GetValue<bool>() is not true || depth is AnilistRecommendationDepth.FirstPage)
                break;

            if (depth is AnilistRecommendationDepth.WhileWellRated && lowestRating < MinimumRatingToKeepPaging)
                break;

            page = await _apiClient.GetRecommendationsPageAsync(anilistAnimeId, (pageInfo["currentPage"]?.GetValue<int>() ?? 1) + 1).ConfigureAwait(false);
        }

        // Anything left over was removed from the anime upstream, or fell past
        // where we stopped paging.
        if (existing.Count > 0)
            _anilistAnimeSuggestions.Delete(existing.Values.ToList());
    }

    private async Task UpdateAnimeCharacters(int anilistAnimeId, JsonNode media)
    {
        var existingCharacters = _anilistAnimeCharacters.GetByAnilistAnimeID(anilistAnimeId).ToDictionary(x => x.AnilistCharacterID);
        var existingVoiceActors = _anilistAnimeCharacterCreators.GetByAnilistAnimeID(anilistAnimeId).ToDictionary(x => (x.AnilistCharacterID, x.AnilistCreatorID));
        var ordering = 0;
        var page = media["characters"];
        while (page is not null)
        {
            if (page["edges"] is JsonArray edges)
            {
                foreach (var edge in edges)
                {
                    if (edge?["node"] is not { } characterNode)
                        continue;

                    var characterId = characterNode["id"]?.GetValue<int>() ?? 0;
                    if (characterId is 0)
                        continue;

                    UpsertCharacter(characterId, characterNode);

                    var role = edge["role"]?.GetValue<string>() ?? string.Empty;
                    if (!existingCharacters.Remove(characterId, out var animeCharacter))
                    {
                        _anilistAnimeCharacters.Save(new Anilist_Anime_Character(anilistAnimeId, characterId, role, ordering));
                    }
                    else if (animeCharacter.Role != role || animeCharacter.Ordering != ordering)
                    {
                        animeCharacter.Role = role;
                        animeCharacter.Ordering = ordering;
                        _anilistAnimeCharacters.Save(animeCharacter);
                    }
                    ordering++;

                    if (edge["voiceActorRoles"] is not JsonArray voiceActorRoles)
                        continue;

                    var voiceActorOrdering = 0;
                    foreach (var voiceActorRole in voiceActorRoles)
                    {
                        if (voiceActorRole?["voiceActor"] is not { } staffNode)
                            continue;

                        var creatorId = staffNode["id"]?.GetValue<int>() ?? 0;
                        if (creatorId is 0)
                            continue;

                        UpsertCreator(creatorId, staffNode);

                        var roleNotes = voiceActorRole["roleNotes"]?.GetValue<string>();
                        var dubGroup = voiceActorRole["dubGroup"]?.GetValue<string>();
                        if (!existingVoiceActors.Remove((characterId, creatorId), out var voiceActor))
                        {
                            _anilistAnimeCharacterCreators.Save(new Anilist_Anime_Character_Creator(anilistAnimeId, characterId, creatorId, voiceActorOrdering) { RoleNotes = roleNotes, DubGroup = dubGroup });
                        }
                        else if (voiceActor.RoleNotes != roleNotes || voiceActor.DubGroup != dubGroup || voiceActor.Ordering != voiceActorOrdering)
                        {
                            voiceActor.RoleNotes = roleNotes;
                            voiceActor.DubGroup = dubGroup;
                            voiceActor.Ordering = voiceActorOrdering;
                            _anilistAnimeCharacterCreators.Save(voiceActor);
                        }
                        voiceActorOrdering++;
                    }
                }
            }

            var pageInfo = page["pageInfo"];
            if (!(pageInfo?["hasNextPage"]?.GetValue<bool>() ?? false))
                break;

            page = await _apiClient.GetCharactersPageAsync(anilistAnimeId, (pageInfo?["currentPage"]?.GetValue<int>() ?? 1) + 1).ConfigureAwait(false);
        }

        if (existingVoiceActors.Count > 0)
            _anilistAnimeCharacterCreators.Delete(existingVoiceActors.Values.ToList());
        if (existingCharacters.Count > 0)
            _anilistAnimeCharacters.Delete(existingCharacters.Values.ToList());
    }

    private async Task UpdateAnimeStaff(int anilistAnimeId, JsonNode media)
    {
        var existingStaff = _anilistAnimeStaff.GetByAnilistAnimeID(anilistAnimeId).ToDictionary(x => (x.AnilistCreatorID, x.Role));
        var ordering = 0;
        var page = media["staff"];
        while (page is not null)
        {
            if (page["edges"] is JsonArray edges)
            {
                foreach (var edge in edges)
                {
                    if (edge?["node"] is not { } staffNode)
                        continue;

                    var creatorId = staffNode["id"]?.GetValue<int>() ?? 0;
                    if (creatorId is 0)
                        continue;

                    UpsertCreator(creatorId, staffNode);

                    var role = edge["role"]?.GetValue<string>() ?? string.Empty;
                    if (!existingStaff.Remove((creatorId, role), out var staff))
                    {
                        _anilistAnimeStaff.Save(new Anilist_Anime_Staff(anilistAnimeId, creatorId, role, ordering));
                    }
                    else if (staff.Ordering != ordering)
                    {
                        staff.Ordering = ordering;
                        _anilistAnimeStaff.Save(staff);
                    }
                    ordering++;
                }
            }

            var pageInfo = page["pageInfo"];
            if (!(pageInfo?["hasNextPage"]?.GetValue<bool>() ?? false))
                break;

            page = await _apiClient.GetStaffPageAsync(anilistAnimeId, (pageInfo?["currentPage"]?.GetValue<int>() ?? 1) + 1).ConfigureAwait(false);
        }

        if (existingStaff.Count > 0)
            _anilistAnimeStaff.Delete(existingStaff.Values.ToList());
    }

    private void UpsertCharacter(int characterId, JsonNode node)
    {
        var character = _anilistCharacters.GetByAnilistCharacterID(characterId) ?? new Anilist_Character(characterId);
        var name = node["name"];
        var updated = character.Anilist_CharacterID is 0;
        updated |= character.UpdateProperty(character.Name, name?["full"]?.GetValue<string>() ?? string.Empty, v => character.Name = v);
        updated |= character.UpdateProperty(character.OriginalName, name?["native"]?.GetValue<string>(), v => character.OriginalName = v);
        updated |= character.UpdateProperty(character.AlternativeNames, ParseStringList(name?["alternative"]), v => character.AlternativeNames = v, StringListEquals);
        updated |= character.UpdateProperty(character.Description, AnilistUtility.StripHtml(node["description"]?.GetValue<string>()), v => character.Description = v);
        updated |= character.UpdateProperty(character.ImagePath, AnilistImageService.ToResourceID(node["image"]?["large"]?.GetValue<string>()), v => character.ImagePath = v);
        updated |= character.UpdateProperty(character.Gender, ParseGender(node["gender"]?.GetValue<string>()), v => character.Gender = v);
        updated |= character.UpdateProperty(character.DateOfBirth, ParsePartialDate(node["dateOfBirth"]), v => character.DateOfBirth = v);
        updated |= character.UpdateProperty(character.Age, node["age"]?.GetValue<string>(), v => character.Age = v);
        updated |= character.UpdateProperty(character.FavoriteCount, node["favourites"]?.GetValue<int?>() ?? 0, v => character.FavoriteCount = v);
        if (!updated)
            return;

        character.LastUpdatedAt = DateTime.Now;
        _anilistCharacters.Save(character);
    }

    private void UpsertCreator(int creatorId, JsonNode node)
    {
        var creator = _anilistCreators.GetByAnilistCreatorID(creatorId) ?? new Anilist_Creator(creatorId);
        var name = node["name"];
        var updated = creator.Anilist_CreatorID is 0;
        updated |= creator.UpdateProperty(creator.Name, name?["full"]?.GetValue<string>() ?? string.Empty, v => creator.Name = v);
        updated |= creator.UpdateProperty(creator.OriginalName, name?["native"]?.GetValue<string>(), v => creator.OriginalName = v);
        updated |= creator.UpdateProperty(creator.AlternativeNames, ParseStringList(name?["alternative"]), v => creator.AlternativeNames = v, StringListEquals);
        updated |= creator.UpdateProperty(creator.Description, AnilistUtility.StripHtml(node["description"]?.GetValue<string>()), v => creator.Description = v);
        updated |= creator.UpdateProperty(creator.ImagePath, AnilistImageService.ToResourceID(node["image"]?["large"]?.GetValue<string>()), v => creator.ImagePath = v);
        updated |= creator.UpdateProperty(creator.Language, node["languageV2"]?.GetValue<string>(), v => creator.Language = v);
        updated |= creator.UpdateProperty(creator.PrimaryOccupations, ParseStringList(node["primaryOccupations"]), v => creator.PrimaryOccupations = v, StringListEquals);
        updated |= creator.UpdateProperty(creator.Gender, ParseGender(node["gender"]?.GetValue<string>()), v => creator.Gender = v);
        updated |= creator.UpdateProperty(creator.DateOfBirth, ParsePartialDate(node["dateOfBirth"]), v => creator.DateOfBirth = v);
        updated |= creator.UpdateProperty(creator.HomeTown, node["homeTown"]?.GetValue<string>(), v => creator.HomeTown = v);
        updated |= creator.UpdateProperty(creator.FavoriteCount, node["favourites"]?.GetValue<int?>() ?? 0, v => creator.FavoriteCount = v);
        if (!updated)
            return;

        creator.LastUpdatedAt = DateTime.Now;
        _anilistCreators.Save(creator);
    }

    #endregion

    #region Anime - Airing Schedule

    /// <summary>
    /// The key of the single schedule AniList supplies per anime. AniList names
    /// neither a station nor a platform, so every broadcast time it knows lives
    /// on one channel-less schedule. The seed migration keys its rows on the
    /// same value, so both write the same schedule.
    /// </summary>
    internal const string AiringScheduleKey = "original";

    /// <summary>
    /// Push the anime's broadcast times to the airing schedule service as the
    /// AniList provider's schedule for the run, with one airing per episode
    /// AniList knows a slot for.
    /// </summary>
    /// <remarks>
    /// A failure is logged and swallowed. The schedule is a view of the
    /// metadata, so losing it must not sink the update that produced it.
    /// </remarks>
    /// <param name="anime">The anime the schedule belongs to.</param>
    /// <param name="schedule">The anime's airing schedule entries, by episode number.</param>
    /// <param name="episodeCount">The number of episodes rows were synthesized for.</param>
    private void UpdateAiringSchedule(Anilist_Anime anime, Dictionary<int, (int ScheduleId, DateTime? AiredAt)> schedule, int episodeCount)
    {
        if (GetAiringScheduleProviderInfo() is not { } info)
            return;

        try
        {
            // An anime AniList knows no broadcast times for gets no schedule of
            // its own, unless it already has one to empty out.
            if (schedule.Count is 0 && GetOwnSchedules(anime, info).Count is 0)
                return;

            var provider = info.Provider;
            var languageCode = string.IsNullOrWhiteSpace(anime.OriginalLanguageCode) ? "unk" : anime.OriginalLanguageCode;
            var scheduleView = _airingScheduleService.AddOrUpdateSchedule(provider, new()
            {
                Series = anime,
                Key = AiringScheduleKey,
                Tracks = [new AiringTrackData(AiringKind.Original, languageCode)],
                FirstEpisodeNumber = 1,
                LastEpisodeNumber = anime.EpisodeCount > 0 ? episodeCount : null,
                IsFinished = anime.ReleasingStatus is AnilistMediaStatus.Finished,
            });

            var episodes = _anilistEpisodes.GetByAnilistAnimeID(anime.AnilistAnimeID).ToDictionary(episode => episode.EpisodeNumber);
            var cutoff = GetAiringRetentionCutoff();
            var airings = schedule
                .OrderBy(entry => entry.Key)
                .Where(entry => episodes.ContainsKey(entry.Key))
                // A run that has aged out whole is dropped here, so the write is an empty line the service clears the schedule on rather than one it rejects.
                .Where(entry => cutoff is not { } window || entry.Value.AiredAt is not { } airedAt || airedAt >= window)
                .Select(entry => new EpisodeAiringData() { Episode = episodes[entry.Key], AiredAt = entry.Value.AiredAt })
                .ToList();
            _airingScheduleService.SetAirings(provider, scheduleView, airings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to update the airing schedule for AniList anime {AnimeId}.", anime.AnilistAnimeID);
        }
    }

    /// <summary>
    /// Remove the schedules the AniList provider owns for the anime, for a
    /// purge of the anime itself.
    /// </summary>
    /// <remarks>
    /// A failure is logged and swallowed, the same way the write is.
    /// </remarks>
    /// <param name="anime">The anime being purged.</param>
    private void RemoveAiringSchedules(Anilist_Anime anime)
    {
        if (GetAiringScheduleProviderInfo() is not { } info)
            return;

        try
        {
            var removed = _airingScheduleService.RemoveSchedulesForSeries(info.Provider, anime);
            if (removed > 0)
                _logger.LogDebug("Removed {Count} airing schedules for AniList anime {AnimeId}.", removed, anime.AnilistAnimeID);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to remove the airing schedules for AniList anime {AnimeId}.", anime.AnilistAnimeID);
        }
    }

    /// <summary>
    /// The registered entry for the AniList airing schedule provider. Every
    /// write is checked against the provider instance it carries, by reference.
    /// </summary>
    /// <returns>
    /// The provider entry, or <c>null</c> when it isn't registered yet, which
    /// is the case until the plugins are initialized.
    /// </returns>
    private AiringScheduleProviderInfo? GetAiringScheduleProviderInfo()
    {
        try
        {
            return _airingScheduleService.GetProviderInfo<AnilistAiringScheduleProvider>();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "The AniList airing schedule provider isn't available. Skipping the airing schedule write.");
            return null;
        }
    }

    /// <summary>
    /// The schedules the AniList provider already owns for the anime itself,
    /// whether or not the provider is enabled.
    /// </summary>
    /// <param name="anime">The anime to read the schedules for.</param>
    /// <param name="info">The registered entry for the provider.</param>
    /// <returns>The schedules.</returns>
    private IReadOnlyList<IAiringSchedule> GetOwnSchedules(Anilist_Anime anime, AiringScheduleProviderInfo info)
        => _airingScheduleService.GetSchedulesForSeries(anime, new() { ProviderID = info.ID, IncludeDisabled = true, LinkedEntitySchedules = false });

    /// <summary>
    /// The oldest slot worth submitting while automatic cleanup is on, since
    /// anything older is only removed again by the next sweep.
    /// </summary>
    /// <returns>The cutoff in UTC, or <c>null</c> when every slot is submitted.</returns>
    private DateTime? GetAiringRetentionCutoff()
    {
        var settings = _airingScheduleSettings.Load();
        if (!settings.AutoCleanup)
            return null;

        return DateTime.UtcNow.AddMonths(-Math.Max(settings.RetentionMonths, AiringScheduleServiceSettings.MinimumRetentionMonths));
    }

    #endregion

    #region Anime - Images

    /// <inheritdoc/>
    public async Task DownloadAllAnimeImages(int anilistAnimeId, bool forceDownload = false)
    {
        using (await _entityLock.GetLockForEntityAsync(DataEntityType.Anime, anilistAnimeId, "images", "Update").ConfigureAwait(false))
        {
            var anime = _anilistAnime.GetByAnilistAnimeID(anilistAnimeId);
            if (anime is null)
                return;

            _logger.LogDebug("Registering images for AniList anime {AnimeTitle} (Anime={AnimeId})", anime.PreferredTitle, anilistAnimeId);

            var settings = _settingsProvider.GetSettings();
            await _imageService.SyncImageOfType(anime.CoverImagePath, ImageEntityType.Primary, anime, settings.Anilist.AutoDownloadPosters, forceDownload).ConfigureAwait(false);
            await _imageService.SyncImageOfType(anime.BannerImagePath, ImageEntityType.Banner, anime, settings.Anilist.AutoDownloadBanners, forceDownload).ConfigureAwait(false);

            var characters = anime.Characters
                .Select(xref => xref.Character)
                .Where(character => character is not null)
                .Select(character => character!)
                .DistinctBy(character => character.AnilistCharacterID)
                .ToList();
            foreach (var character in characters)
                await _imageService.SyncImageOfType(character.ImagePath, ImageEntityType.Primary, character, settings.Anilist.AutoDownloadCharacters, forceDownload).ConfigureAwait(false);

            var creators = anime.Characters
                .SelectMany(xref => xref.Creators)
                .Concat(anime.Staff.Select(xref => xref.Creator).Where(creator => creator is not null).Select(creator => creator!))
                .DistinctBy(creator => creator.AnilistCreatorID)
                .ToList();
            foreach (var creator in creators)
                await _imageService.SyncImageOfType(creator.ImagePath, ImageEntityType.Primary, creator, settings.Anilist.AutoDownloadStaff, forceDownload).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task ScheduleDownloadAllAnimeImages(int anilistAnimeId, bool forceDownload = false)
    {
        if (anilistAnimeId is 0)
            return;

        await _scheduler.StartJob<DownloadAnilistAnimeImagesJob>(c =>
        {
            c.AnilistAnimeID = anilistAnimeId;
            c.ForceDownload = forceDownload;
        }).ConfigureAwait(false);
    }

    #endregion

    #region Anime - Purge

    /// <inheritdoc/>
    public async Task PurgeAllUnusedAnime(DateTime? olderThan = null)
    {
        var usedAnimeIds = _xrefAnidbAnilistAnime.GetAll().Select(x => x.AnilistAnimeID).ToHashSet();
        var unusedAnime = _anilistAnime.GetAll()
            .Where(a => !usedAnimeIds.Contains(a.AnilistAnimeID) && (olderThan is null || a.LastUpdatedAt < olderThan.Value))
            .ToList();
        _logger.LogInformation("Scheduling {Count} unused AniList anime to be purged.", unusedAnime.Count);
        foreach (var anime in unusedAnime)
        {
            await _scheduler.StartJob<PurgeAnilistAnimeJob>(c =>
            {
                c.AnilistAnimeID = anime.AnilistAnimeID;
                c.AnimeTitle = anime.PreferredTitle;
            }).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task SchedulePurgeOfAnime(int anilistAnimeId)
    {
        if (anilistAnimeId is 0)
            return;

        await _scheduler.StartJob<PurgeAnilistAnimeJob>(c => c.AnilistAnimeID = anilistAnimeId).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task PurgeAnime(int anilistAnimeId)
    {
        using (await _entityLock.GetLockForEntityAsync(DataEntityType.Anime, anilistAnimeId, "metadata", "Purge").ConfigureAwait(false))
        {
            _logger.LogInformation("Purging AniList anime {AnimeId}", anilistAnimeId);

            var anime = _anilistAnime.GetByAnilistAnimeID(anilistAnimeId);
            if (anime is not null)
            {
                _imageService.PurgeImages(anime);
                RemoveAiringSchedules(anime);
            }

            var xrefs = _xrefAnidbAnilistAnime.GetByAnilistAnimeID(anilistAnimeId);
            if (xrefs.Count > 0)
                _xrefAnidbAnilistAnime.Delete(xrefs);

            var episodeXrefs = _xrefAnidbAnilistEpisodes.GetByAnilistAnimeID(anilistAnimeId);
            if (episodeXrefs.Count > 0)
                _xrefAnidbAnilistEpisodes.Delete(episodeXrefs);

            var episodes = _anilistEpisodes.GetByAnilistAnimeID(anilistAnimeId);
            if (episodes.Count > 0)
                _anilistEpisodes.Delete(episodes);

            var animeTags = _anilistAnimeTags.GetByAnilistAnimeID(anilistAnimeId);
            if (animeTags.Count > 0)
                _anilistAnimeTags.Delete(animeTags);

            var externalLinks = _anilistAnimeExternalLinks.GetByAnilistAnimeID(anilistAnimeId);
            if (externalLinks.Count > 0)
                _anilistAnimeExternalLinks.Delete(externalLinks);

            var animeStudios = _anilistAnimeStudios.GetByAnilistAnimeID(anilistAnimeId);
            if (animeStudios.Count > 0)
                _anilistAnimeStudios.Delete(animeStudios);

            var relations = _anilistAnimeRelations.GetByAnilistAnimeID(anilistAnimeId);
            if (relations.Count > 0)
                _anilistAnimeRelations.Delete(relations);

            var voiceActors = _anilistAnimeCharacterCreators.GetByAnilistAnimeID(anilistAnimeId);
            if (voiceActors.Count > 0)
                _anilistAnimeCharacterCreators.Delete(voiceActors);

            var characters = _anilistAnimeCharacters.GetByAnilistAnimeID(anilistAnimeId);
            if (characters.Count > 0)
                _anilistAnimeCharacters.Delete(characters);

            var staff = _anilistAnimeStaff.GetByAnilistAnimeID(anilistAnimeId);
            if (staff.Count > 0)
                _anilistAnimeStaff.Delete(staff);

            if (anime is not null)
                _anilistAnime.Delete(anime);
        }
    }

    #endregion

    #region Rate Limiting

    /// <inheritdoc/>
    public AnilistRateLimitPauseStatus GetPauseStatus()
    {
        var (isPaused, remaining, remainingRequests) = _rateLimiter.GetPauseSnapshot();
        return new() { IsPaused = isPaused, RemainingPauseTime = remaining, RemainingRequests = remainingRequests };
    }

    #endregion

    #region Search/Matching

    /// <inheritdoc/>
    public async Task ScheduleSearchForMatch(int anidbId, bool force)
    {
        await _scheduler.StartJob<SearchAnilistForMatchJob>(c =>
        {
            c.AnimeID = anidbId;
            c.ForceRefresh = force;
        }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ScanForMatches()
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.Anilist.AutoLink)
            return;

        var toSearch = new List<int>();
        foreach (var series in _animeSeries.GetAll())
        {
            if (series.IsAnilistAutoMatchingDisabled)
                continue;

            if (series.AniDB_Anime is not { } anime)
                continue;

            if (anime.IsRestricted && !settings.Anilist.AutoLinkRestricted)
                continue;

            if (_xrefAnidbAnilistAnime.GetByAnidbAnimeID(anime.AnimeID).Count > 0)
                continue;

            _logger.LogTrace("Found anime without AniList association: {MainTitle}", anime.MainTitle);
            toSearch.Add(anime.AnimeID);
        }

        _logger.LogInformation("Scheduling {Count} anime to be searched for AniList matches.", toSearch.Count);
        foreach (var animeId in toSearch)
            await _scheduler.StartJob<SearchAnilistForMatchJob>(c => c.AnimeID = animeId).ConfigureAwait(false);
    }

    #endregion
}
