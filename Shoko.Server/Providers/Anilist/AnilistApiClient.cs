using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Anilist;

#nullable enable
namespace Shoko.Server.Providers.Anilist;

/// <summary>
/// Client for the AniList GraphQL API. Every request goes through
/// <see cref="AnilistRateLimiter"/>, honours the quota headers AniList sends
/// on each response, and retries a 429 or a timeout only after the wait the
/// server asked for. A server error is never retried here: it trips the
/// breaker and surfaces as a transient <see cref="AnilistApiException"/>, so
/// the job re-queues and waits for the pause to lift instead of adding load
/// to a struggling upstream.
/// </summary>
public class AnilistApiClient
{
    /// <summary>
    /// The maximum page size AniList allows.
    /// </summary>
    public const int MaxPageSize = 50;

    private const int MaxRateLimitRetries = 3;

    private const int MaxTimeoutRetries = 2;

    private readonly ILogger<AnilistApiClient> _logger;

    private readonly IHttpClientFactory _httpClientFactory;

    private readonly AnilistRateLimiter _rateLimiter;

    public AnilistApiClient(ILogger<AnilistApiClient> logger, IHttpClientFactory httpClientFactory, AnilistRateLimiter rateLimiter)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _rateLimiter = rateLimiter;
    }

    #region GraphQL Fragments

    private const string MediaFragment = """
        fragment MediaFields on Media {
          id
          idMal
          type
          format
          title { english native romaji }
          synonyms
          description(asHtml: false)
          coverImage { color extraLarge }
          bannerImage
          startDate { year month day }
          endDate { year month day }
          status
          source
          isAdult
          isLicensed
          season
          seasonYear
          countryOfOrigin
          averageScore
          meanScore
          favourites
          popularity
          episodes
          duration
          genres
          updatedAt
          trailer { id site }
          externalLinks { id url site siteId type language }
        }
        """;

    private const string CharacterFragment = """
        fragment CharacterFields on Character {
          id
          name { full native alternative }
          image { large }
          description(asHtml: false)
          gender
          dateOfBirth { year month day }
          age
          favourites
          siteUrl
        }
        """;

    private const string StaffFragment = """
        fragment StaffFields on Staff {
          id
          name { full native alternative }
          image { large }
          description(asHtml: false)
          languageV2
          primaryOccupations
          gender
          dateOfBirth { year month day }
          homeTown
          favourites
          siteUrl
        }
        """;

    private const string SchedulePage = """
        airingSchedule(notYetAired: false, perPage: 50, page: $schedulePage) {
          nodes { id episode airingAt }
          pageInfo { currentPage hasNextPage }
        }
        """;

    private const string CharactersPage = """
        characters(perPage: 25, page: $characterPage, sort: [ROLE, RELEVANCE, ID]) {
          pageInfo { currentPage hasNextPage }
          edges {
            role
            voiceActorRoles(sort: [RELEVANCE, ID]) {
              roleNotes
              dubGroup
              voiceActor { ...StaffFields }
            }
            node { ...CharacterFields }
          }
        }
        """;

    private const string StaffPage = """
        staff(perPage: 25, page: $staffPage, sort: [RELEVANCE, ID]) {
          pageInfo { currentPage hasNextPage }
          edges {
            role
            node { ...StaffFields }
          }
        }
        """;

    /// <summary>
    /// Everything about an anime in one round-trip: metadata, the first page
    /// of the airing schedule, characters and staff, plus tags, studios and
    /// relations, which are not paginated.
    /// </summary>
    private static readonly string AnimeQuery = $$"""
        query ($id: Int, $schedulePage: Int, $characterPage: Int, $staffPage: Int) {
          Media(id: $id, type: ANIME) {
            ...MediaFields
            {{SchedulePage}}
            studios(sort: [ID]) {
              edges {
                isMain
                node { id name isAnimationStudio favourites siteUrl }
              }
            }
            tags { id name category description isAdult isGeneralSpoiler isMediaSpoiler rank }
            relations {
              edges {
                relationType(version: 2)
                node { id type format title { romaji } }
              }
            }
            {{CharactersPage}}
            {{StaffPage}}
          }
        }
        {{MediaFragment}}
        {{CharacterFragment}}
        {{StaffFragment}}
        """;

    private static readonly string AiringScheduleQuery = $$"""
        query ($id: Int, $schedulePage: Int) {
          Media(id: $id, type: ANIME) {
            {{SchedulePage}}
          }
        }
        """;

    private static readonly string CharactersQuery = $$"""
        query ($id: Int, $characterPage: Int) {
          Media(id: $id, type: ANIME) {
            {{CharactersPage}}
          }
        }
        {{CharacterFragment}}
        {{StaffFragment}}
        """;

    private static readonly string StaffQuery = $$"""
        query ($id: Int, $staffPage: Int) {
          Media(id: $id, type: ANIME) {
            {{StaffPage}}
          }
        }
        {{StaffFragment}}
        """;

    #endregion

    #region Public Methods

    /// <summary>
    /// Get an anime with the first page of its schedule, characters and staff.
    /// </summary>
    /// <returns>The <c>Media</c> node, or <see langword="null"/> if AniList has no such anime.</returns>
    public Task<JsonNode?> GetAnimeByIdAsync(int anilistAnimeId, CancellationToken cancellationToken = default)
        => ExecuteAndSelectAsync(AnimeQuery, new() { ["id"] = anilistAnimeId, ["schedulePage"] = 1, ["characterPage"] = 1, ["staffPage"] = 1 }, "Media", $"Get anime {anilistAnimeId}", cancellationToken);

    /// <summary>
    /// Get one page of the airing schedule for an anime.
    /// </summary>
    public async Task<JsonNode?> GetAiringSchedulePageAsync(int anilistAnimeId, int page, CancellationToken cancellationToken = default)
        => (await ExecuteAndSelectAsync(AiringScheduleQuery, new() { ["id"] = anilistAnimeId, ["schedulePage"] = page }, "Media", $"Get airing schedule page {page} for anime {anilistAnimeId}", cancellationToken).ConfigureAwait(false))?["airingSchedule"];

    /// <summary>
    /// Get one page of the characters (with their voice actors) for an anime.
    /// </summary>
    public async Task<JsonNode?> GetCharactersPageAsync(int anilistAnimeId, int page, CancellationToken cancellationToken = default)
        => (await ExecuteAndSelectAsync(CharactersQuery, new() { ["id"] = anilistAnimeId, ["characterPage"] = page }, "Media", $"Get characters page {page} for anime {anilistAnimeId}", cancellationToken).ConfigureAwait(false))?["characters"];

    /// <summary>
    /// Get one page of the staff for an anime.
    /// </summary>
    public async Task<JsonNode?> GetStaffPageAsync(int anilistAnimeId, int page, CancellationToken cancellationToken = default)
        => (await ExecuteAndSelectAsync(StaffQuery, new() { ["id"] = anilistAnimeId, ["staffPage"] = page }, "Media", $"Get staff page {page} for anime {anilistAnimeId}", cancellationToken).ConfigureAwait(false))?["staff"];

    /// <summary>
    /// Search for anime. Every filter in <paramref name="options"/> is applied
    /// by AniList, so only the requested page is transferred.
    /// </summary>
    /// <returns>The <c>Page</c> node with <c>pageInfo</c> and <c>media</c>, or <see langword="null"/> on an empty response.</returns>
    public Task<JsonNode?> SearchAnimeAsync(AnilistSearchOptions options, CancellationToken cancellationToken = default)
    {
        var variables = new Dictionary<string, object?>
        {
            ["search"] = options.Query,
            ["page"] = Math.Max(options.Page, 1),
            ["perPage"] = Math.Clamp(options.PageSize, 1, MaxPageSize),
        };
        var declarations = new List<string> { "$search: String", "$page: Int", "$perPage: Int" };
        var arguments = new List<string> { "search: $search", "type: ANIME", "sort: [SEARCH_MATCH]" };

        if (!options.IncludeRestricted)
        {
            variables["isAdult"] = false;
            declarations.Add("$isAdult: Boolean");
            arguments.Add("isAdult: $isAdult");
        }

        if (options.Year is > 0)
        {
            // FuzzyDateInt is YYYYMMDD; the comparisons are exclusive, so bracket the whole year.
            variables["startAfter"] = (options.Year.Value - 1) * 10000 + 1231;
            variables["startBefore"] = (options.Year.Value + 1) * 10000 + 101;
            declarations.Add("$startAfter: FuzzyDateInt");
            declarations.Add("$startBefore: FuzzyDateInt");
            arguments.Add("startDate_greater: $startAfter");
            arguments.Add("startDate_lesser: $startBefore");
        }

        if (options.Season is { } season)
        {
            variables["season"] = AnilistUtility.ToSeason(season);
            declarations.Add("$season: MediaSeason");
            arguments.Add("season: $season");
        }

        if (options.SeasonYear is > 0)
        {
            variables["seasonYear"] = options.SeasonYear.Value;
            declarations.Add("$seasonYear: Int");
            arguments.Add("seasonYear: $seasonYear");
        }

        if (options.Types is { Count: > 0 } types)
        {
            var formats = types.SelectMany(AnilistUtility.ToFormats).Distinct().ToList();
            if (formats.Count > 0)
            {
                variables["formats"] = formats;
                declarations.Add("$formats: [MediaFormat]");
                arguments.Add("format_in: $formats");
            }
        }

        var query = $$"""
            query ({{string.Join(", ", declarations)}}) {
              Page(page: $page, perPage: $perPage) {
                pageInfo { currentPage lastPage hasNextPage total }
                media({{string.Join(", ", arguments)}}) {
                  ...MediaFields
                }
              }
            }
            {{MediaFragment}}
            """;
        return ExecuteAndSelectAsync(query, variables, "Page", $"Search anime \"{options.Query}\" (page {Math.Max(options.Page, 1)})", cancellationToken);
    }

    #endregion

    #region Private Methods

    private async Task<JsonNode?> ExecuteAndSelectAsync(string query, Dictionary<string, object?> variables, string rootField, string displayName, CancellationToken cancellationToken)
    {
        var result = await ExecuteQueryAsync(query, variables, displayName, cancellationToken).ConfigureAwait(false);
        return result?["data"]?[rootField];
    }

    /// <summary>
    /// Execute a GraphQL query. Returns <see langword="null"/> only when AniList
    /// reports the requested entity does not exist. Every other failure is
    /// either retried or thrown as <see cref="AnilistApiException"/>, so
    /// callers never mistake an outage for a missing entity.
    /// </summary>
    private async Task<JsonNode?> ExecuteQueryAsync(string query, Dictionary<string, object?> variables, string displayName, CancellationToken cancellationToken)
    {
        // Same shape as the TMDB call tracing, so a slow update can be read off the log: how long the
        // limiter held the call, how long AniList took, and how many attempts it needed.
        var scheduledAt = DateTime.Now;
        var attempts = 0;
        var waitTime = TimeSpan.Zero;
        _logger.LogTrace("Scheduled call: {DisplayName}", displayName);
        var result = await _rateLimiter.EnsureRateAsync(async () =>
        {
            waitTime = DateTime.Now - scheduledAt;
            _logger.LogTrace("Executing call: {DisplayName} (Waited {Waited}ms)", displayName, waitTime.TotalMilliseconds);
            var rateLimitRetries = 0;
            var timeoutRetries = 0;
            while (true)
            {
                try
                {
                    ++attempts;
                    var (result, retry) = await SendAsync(query, variables, cancellationToken).ConfigureAwait(false);
                    if (!retry)
                        return result;

                    if (++rateLimitRetries > MaxRateLimitRetries)
                        throw new AnilistApiException("AniList rate limit retry budget exhausted.", HttpStatusCode.TooManyRequests, isTransient: true);

                    // The limiter has been told about the backoff; wait out what the server asked for before trying again.
                    await Task.Delay((_rateLimiter.RemainingPauseTime ?? TimeSpan.FromSeconds(60)) + AnilistRateLimiter.Jitter(), cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    if (++timeoutRetries > MaxTimeoutRetries)
                        throw new AnilistApiException("AniList request timed out.", isTransient: true);

                    _logger.LogTrace("AniList request timed out. Retrying ({Retry}/{Max}).", timeoutRetries, MaxTimeoutRetries);
                }
            }
        }).ConfigureAwait(false);
        var executed = DateTime.Now - scheduledAt - waitTime;
        _logger.LogTrace("Completed call: {DisplayName} (Waited {Waited}ms, Executed: {Delta}ms, {Attempts} attempts)", displayName, waitTime.TotalMilliseconds, executed.TotalMilliseconds, attempts);
        return result;
    }

    private async Task<(JsonNode? Result, bool Retry)> SendAsync(string query, Dictionary<string, object?> variables, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient("Anilist");
        using var content = new StringContent(JsonSerializer.Serialize(new { query, variables }), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(string.Empty, content, cancellationToken).ConfigureAwait(false);

        RecordQuotaHeaders(response);

        var retryAfter = response.Headers.RetryAfter?.Delta
            ?? (response.Headers.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : (TimeSpan?)null)
            ?? (TryGetHeaderInt(response, "X-RateLimit-Reset", out var resetSeconds) ? DateTimeOffset.FromUnixTimeSeconds(resetSeconds) - DateTimeOffset.UtcNow : (TimeSpan?)null);
        if (retryAfter is { } negative && negative < TimeSpan.Zero)
            retryAfter = null;

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning("AniList rate limit hit. Retrying after {RetryAfter}s.", (retryAfter ?? TimeSpan.FromSeconds(60)).TotalSeconds);
            _rateLimiter.NotifyRateLimitExceeded(retryAfter);
            return (null, true);
        }

        if ((int)response.StatusCode >= 500)
        {
            _logger.LogWarning("AniList returned {StatusCode} {Reason}. Pausing AniList jobs.", (int)response.StatusCode, response.ReasonPhrase);
            _rateLimiter.Notify5xxError();
            if (retryAfter is { } serverRetryAfter)
                _rateLimiter.NotifyRateLimitExceeded(serverRetryAfter);
            throw new AnilistApiException($"AniList returned {(int)response.StatusCode} {response.ReasonPhrase}.", response.StatusCode, isTransient: true);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        JsonNode? result;
        try
        {
            result = JsonNode.Parse(body);
        }
        catch (JsonException ex)
        {
            throw new AnilistApiException("AniList returned a response that isn't valid JSON.", response.StatusCode, innerException: ex);
        }

        if (result?["errors"] is JsonArray { Count: > 0 } errorArray)
        {
            var errors = errorArray
                .Select(error => error?["message"]?.GetValue<string>())
                .Where(message => !string.IsNullOrEmpty(message))
                .Select(message => message!)
                .ToList();
            var statuses = errorArray
                .Select(error => error?["status"]?.GetValue<int?>())
                .Where(status => status.HasValue)
                .Select(status => status!.Value)
                .ToList();

            // A missing entity is a normal outcome, not a failure.
            if (statuses.Count > 0 && statuses.All(status => status == 404))
                return (null, false);

            if (statuses.Any(status => status == 429))
            {
                _rateLimiter.NotifyRateLimitExceeded(retryAfter);
                return (null, true);
            }

            if (statuses.Any(status => status >= 500))
            {
                _logger.LogWarning("AniList reported a server error: {Errors}. Pausing AniList jobs.", string.Join("; ", errors));
                _rateLimiter.Notify5xxError();
                throw new AnilistApiException($"AniList reported a server error: {string.Join("; ", errors)}", response.StatusCode, errors, isTransient: true);
            }

            throw new AnilistApiException($"AniList rejected the request: {string.Join("; ", errors)}", response.StatusCode, errors);
        }

        if (!response.IsSuccessStatusCode)
            throw new AnilistApiException($"AniList returned {(int)response.StatusCode} {response.ReasonPhrase}.", response.StatusCode);

        _rateLimiter.NotifySuccess();
        return (result, false);
    }

    private void RecordQuotaHeaders(HttpResponseMessage response)
    {
        if (!TryGetHeaderInt(response, "X-RateLimit-Remaining", out var remaining))
            return;

        DateTimeOffset? resetAt = TryGetHeaderInt(response, "X-RateLimit-Reset", out var reset)
            ? DateTimeOffset.FromUnixTimeSeconds(reset)
            : null;
        int? limit = TryGetHeaderInt(response, "X-RateLimit-Limit", out var limitValue) ? limitValue : null;
        _rateLimiter.NotifyQuota(limit, remaining, resetAt);
    }

    private static bool TryGetHeaderInt(HttpResponseMessage response, string name, out int value)
    {
        value = 0;
        return response.Headers.TryGetValues(name, out var values)
            && int.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    #endregion
}
