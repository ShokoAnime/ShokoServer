using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.Trakt;

[DatabaseRequired]
[NetworkRequired]
[DisallowConcurrencyGroup(ConcurrencyGroups.Trakt)]
[JobKeyGroup(JobKeyGroup.Trakt)]
public class SyncTraktCollectionSeriesJob : BaseJob
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly TraktTVHelper _helper;
    private string _seriesName;
    public int AnimeSeriesID { get; set; }

    public override string TypeName => "Sync Series to Trakt Collection";
    public override string Title => "Syncing Series to Trakt Collection";

    public override void PostInit()
    {
        _seriesName = RepoFactory.AnimeSeries?.GetByID(AnimeSeriesID)?.GetSeriesName() ?? AnimeSeriesID.ToString();
    }

    public override Dictionary<string, object> Details => new() { { "Anime", _seriesName } };

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job} -> Series: {Name}", nameof(SyncTraktCollectionSeriesJob), _seriesName);
        var settings = _settingsProvider.GetSettings();
        if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken)) return Task.CompletedTask;

        var series = RepoFactory.AnimeSeries.GetByID(AnimeSeriesID);
        if (series == null)
        {
            _logger.LogError("Could not find anime series: {AnimeSeriesID}", AnimeSeriesID);
            return Task.CompletedTask;
        }

        _helper.SyncCollectionToTrakt_Series(series);

        return Task.CompletedTask;
    }

    public SyncTraktCollectionSeriesJob(TraktTVHelper helper, ISettingsProvider settingsProvider)
    {
        _helper = helper;
        _settingsProvider = settingsProvider;
    }

    protected SyncTraktCollectionSeriesJob() { }
}
