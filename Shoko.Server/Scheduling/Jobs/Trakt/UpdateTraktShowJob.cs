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
public class UpdateTraktShowJob : BaseJob
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly TraktTVHelper _helper;
    private string _showName;
    public string TraktShowID { get; set; }

    public override string TypeName => "Update Trakt Show data";
    public override string Title => "Updating Trakt Show data";
    
    public override void PostInit()
    {
        _showName = RepoFactory.Trakt_Show?.GetByTraktSlug(TraktShowID)?.Title ?? TraktShowID;
    }

    public override Dictionary<string, object> Details => new() { { "Show", _showName } };
    
    public override Task Process()
    {
        _logger.LogInformation("Processing {Job} -> Show: {Name}", nameof(UpdateTraktShowJob), _showName);
        var settings = _settingsProvider.GetSettings();
        if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken)) return Task.CompletedTask;

        var show = RepoFactory.Trakt_Show.GetByTraktSlug(TraktShowID);
        if (show == null)
        {
            _logger.LogError("Could not find trakt show: {TraktShowID}", TraktShowID);
            return Task.CompletedTask;
        }

        _helper.UpdateAllInfo(TraktShowID);

        return Task.CompletedTask;
    }
    
    public UpdateTraktShowJob(TraktTVHelper helper, ISettingsProvider settingsProvider)
    {
        _helper = helper;
        _settingsProvider = settingsProvider;
    }

    protected UpdateTraktShowJob() { }
}
