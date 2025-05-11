using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Services;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBHttpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_HTTP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class GetRemoteAniDBAnimeJob : BaseJob<SVR_AniDB_Anime>
{
    private readonly ISettingsProvider _settingsProvider;

    private readonly AnidbService _aniDBService;

    private readonly AniDBTitleHelper _titleHelper;

    private string _animeName;

    /// <summary>
    /// The ID of the AniDB anime to update.
    /// </summary>
    public int AnimeID { get; set; }

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
    public bool SkipTmdbUpdate { get; set; }

    /// <summary>
    /// Current depth of recursion.
    /// </summary>
    public int RelDepth { get; set; }

    public AnidbRefreshMethod RefreshMethod
    {
        get
        {
            var refreshMethod = AnidbRefreshMethod.Remote;
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
            if (SkipTmdbUpdate)
                refreshMethod |= AnidbRefreshMethod.SkipTmdbUpdate;
            return refreshMethod;
        }
        set
        {
            if (value is AnidbRefreshMethod.None)
                return;

            // Use the defaults based on settings.
            if (value is AnidbRefreshMethod.Auto)
            {
                var settings = _settingsProvider.GetSettings();
                DownloadRelations = settings.AutoGroupSeries || settings.AniDb.DownloadRelatedAnime;
                CreateSeriesEntry = settings.AniDb.AutomaticallyImportSeries;
            }
            // Toggle everything manually.
            else
            {
                PreferCacheOverRemote = value.HasFlag(AnidbRefreshMethod.PreferCacheOverRemote);
                DeferToRemoteIfUnsuccessful = value.HasFlag(AnidbRefreshMethod.DeferToRemoteIfUnsuccessful);
                IgnoreTimeCheck = value.HasFlag(AnidbRefreshMethod.IgnoreTimeCheck);
                IgnoreHttpBans = value.HasFlag(AnidbRefreshMethod.IgnoreHttpBans);
                DownloadRelations = value.HasFlag(AnidbRefreshMethod.DownloadRelations);
                CreateSeriesEntry = value.HasFlag(AnidbRefreshMethod.CreateShokoSeries);
                SkipTmdbUpdate = value.HasFlag(AnidbRefreshMethod.SkipTmdbUpdate);
            }
        }
    }

    public override void PostInit()
    {
        // We have the title helper. May as well use it to provide better info for the user
        _animeName = RepoFactory.AniDB_Anime?.GetByAnimeID(AnimeID)?.PreferredTitle ?? _titleHelper.SearchAnimeID(AnimeID)?.PreferredTitle;
    }

    public override string TypeName => "Get AniDB Anime Data (Force Remote)";

    public override string Title => "Getting AniDB Anime Data (Force Remote)";

    public override Dictionary<string, object> Details => _animeName == null
        ? new()
        {
            { "AnimeID", AnimeID },
            { "Remote Only", true },
        }
        : new() {
            { "Anime", _animeName },
            { "AnimeID", AnimeID },
            { "Remote Only", true }
        };

    public override async Task<SVR_AniDB_Anime> Process()
    {
        _logger.LogInformation("Processing {JobName} for {Anime}: AniDB ID {ID}", nameof(GetRemoteAniDBAnimeJob), _animeName ?? AnimeID.ToString(), AnimeID);
        return await _aniDBService.Process(AnimeID, RefreshMethod, RelDepth).ConfigureAwait(false);
    }


    public GetRemoteAniDBAnimeJob(ISettingsProvider settingsProvider, IAniDBService anidbService, AniDBTitleHelper titleHelper)
    {
        _settingsProvider = settingsProvider;
        _aniDBService = (AnidbService)anidbService;
        _titleHelper = titleHelper;
    }

    protected GetRemoteAniDBAnimeJob() { }
}
