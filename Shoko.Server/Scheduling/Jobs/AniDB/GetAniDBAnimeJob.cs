using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Services;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_HTTP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class GetAniDBAnimeJob(ISettingsProvider settingsProvider, AnidbService anidbService, AniDBTitleHelper titleHelper, AniDB_AnimeRepository anidbAnimes) : BaseJob<AniDB_Anime?>, IJobMerge
{
    private string? _animeName;

    /// <summary>
    /// The ID of the AniDB anime to update.
    /// </summary>
    [JobKeyMember]
    public int AnimeID { get; set; }

    /// <summary>
    /// Use the remote AniDB HTTP API.
    /// </summary>
    public bool UseRemote { get; set; } = true;

    /// <summary>
    /// Use the local AniDB HTTP cache.
    /// </summary>
    public bool UseCache { get; set; } = true;

    /// <summary>
    /// Prefer the local AniDB HTTP cache over the remote AniDB HTTP API.
    /// </summary>
    public bool PreferCacheOverRemote { get; set; }

    /// <summary>
    /// Defer to a later remote update if the current update fails.
    /// </summary>
    public bool DeferToRemoteIfUnsuccessful { get; set; } = true;

    /// <summary>
    /// Ignore the time check and forces a refresh even if the anime was
    /// recently updated.
    /// </summary>
    public bool IgnoreTimeCheck { get; set; }

    /// <summary>
    /// Ignore any active HTTP bans and forcefully asks the server for the data.
    /// </summary>
    public bool IgnoreHttpBans { get; set; }

    /// <summary>
    /// Download related anime until the maximum depth is reached.
    /// </summary>
    public bool DownloadRelations { get; set; }

    /// <summary>
    /// Create a Shoko series entry if one does not exist.
    /// </summary>
    public bool CreateSeriesEntry { get; set; }

    /// <summary>
    /// Skip updating related TMDB entities after update.
    /// </summary>
    public bool SkipSupplementaryUpdate { get; set; }

    /// <summary>
    /// Current depth of recursion.
    /// </summary>
    public int RelDepth { get; set; }

    public AnidbRefreshMethod RefreshMethod
    {
        get
        {
            var refreshMethod = AnidbRefreshMethod.None;
            if (UseCache)
                refreshMethod |= AnidbRefreshMethod.Cache;
            if (UseRemote)
                refreshMethod |= AnidbRefreshMethod.Remote;
            if (PreferCacheOverRemote)
                refreshMethod |= AnidbRefreshMethod.PreferCacheOverRemote;
            if (DeferToRemoteIfUnsuccessful)
                refreshMethod |= AnidbRefreshMethod.DeferToRemoteIfUnsuccessful;
            if (IgnoreTimeCheck)
                refreshMethod |= AnidbRefreshMethod.IgnoreTimeCheck;
            if (IgnoreHttpBans)
                refreshMethod |= AnidbRefreshMethod.IgnoreHttpBans;
            if (DownloadRelations)
                refreshMethod |= AnidbRefreshMethod.DownloadRelations;
            if (CreateSeriesEntry)
                refreshMethod |= AnidbRefreshMethod.CreateShokoSeries;
            if (SkipSupplementaryUpdate)
                refreshMethod |= AnidbRefreshMethod.SkipSupplementaryUpdate;
            return refreshMethod;
        }
        set
        {
            if (value is AnidbRefreshMethod.None)
                return;

            // Use the defaults based on settings.
            if (value is AnidbRefreshMethod.Auto)
            {
                var settings = settingsProvider.GetSettings();
                DownloadRelations = settings.AutoGroupSeries || settings.AniDb.DownloadRelatedAnime;
                CreateSeriesEntry = settings.AniDb.AutomaticallyImportSeries;
            }
            // Toggle everything manually.
            else
            {
                UseCache = value.HasFlag(AnidbRefreshMethod.Cache);
                UseRemote = value.HasFlag(AnidbRefreshMethod.Remote);
                PreferCacheOverRemote = value.HasFlag(AnidbRefreshMethod.PreferCacheOverRemote);
                DeferToRemoteIfUnsuccessful = value.HasFlag(AnidbRefreshMethod.DeferToRemoteIfUnsuccessful);
                IgnoreTimeCheck = value.HasFlag(AnidbRefreshMethod.IgnoreTimeCheck);
                IgnoreHttpBans = value.HasFlag(AnidbRefreshMethod.IgnoreHttpBans);
                DownloadRelations = value.HasFlag(AnidbRefreshMethod.DownloadRelations);
                CreateSeriesEntry = value.HasFlag(AnidbRefreshMethod.CreateShokoSeries);
                SkipSupplementaryUpdate = value.HasFlag(AnidbRefreshMethod.SkipSupplementaryUpdate);
            }
        }
    }

    public override string TypeName => "Get AniDB Anime Data";

    public override string Title => "Getting AniDB Anime Data";

    public override Dictionary<string, object> Details => _animeName == null ? new()
    {
        {
            "AnimeID", AnimeID
        }
    } : new() {
        {
            "Anime", _animeName
        }
    };

    public override void PostInit()
    {
        // We have the title helper. May as well use it to provide better info for the user
        _animeName = anidbAnimes.GetByAnimeID(AnimeID)?.Title ?? titleHelper.SearchAnimeID(AnimeID)?.Title;
    }

    public bool TryMerge(IQueueJob incoming)
    {
        if (incoming is not GetAniDBAnimeJob other) return false;
        var changed = false;
        // OR-semantics: if either request enables a flag, the merged job enables it
        if (!DownloadRelations && other.DownloadRelations) { DownloadRelations = true; changed = true; }
        if (!IgnoreTimeCheck && other.IgnoreTimeCheck) { IgnoreTimeCheck = true; changed = true; }
        if (!IgnoreHttpBans && other.IgnoreHttpBans) { IgnoreHttpBans = true; changed = true; }
        if (!CreateSeriesEntry && other.CreateSeriesEntry) { CreateSeriesEntry = true; changed = true; }
        if (!UseRemote && other.UseRemote) { UseRemote = true; changed = true; }
        if (!UseCache && other.UseCache) { UseCache = true; changed = true; }
        // AND-semantics: SkipSupplementaryUpdate=false means "do update TMDB" — false wins
        if (SkipSupplementaryUpdate && !other.SkipSupplementaryUpdate) { SkipSupplementaryUpdate = false; changed = true; }
        // MIN-semantics: lower RelDepth = can recurse deeper
        if (other.RelDepth < RelDepth) { RelDepth = other.RelDepth; changed = true; }
        return changed;
    }

    public override async Task<AniDB_Anime?> Process()
    {
        _logger.LogInformation("Processing {JobName} for {Anime}: AniDB ID {ID}", nameof(GetAniDBAnimeJob), _animeName ?? AnimeID.ToString(), AnimeID);
        return await anidbService.Process(AnimeID, RefreshMethod, RelDepth).ConfigureAwait(false);
    }
}
