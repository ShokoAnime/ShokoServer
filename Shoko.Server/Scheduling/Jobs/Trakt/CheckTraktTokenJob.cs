using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Settings;
using Shoko.Server.Providers.TraktTV;

namespace Shoko.Server.Scheduling.Jobs.Trakt;

[DatabaseRequired]
[NetworkRequired]
[DisallowConcurrencyGroup(ConcurrencyGroups.Trakt)]
[JobKeyGroup(JobKeyGroup.Trakt)]
public class CheckTraktTokenJob : BaseJob
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly TraktTVHelper _traktHelper;

    public bool ForceRefresh;
    public override string TypeName => "Check and Refresh Trakt Token";
    public override string Title => "Checking and Refreshing Trakt Token";

    public override Task Process()
    {
        try
        {
            _logger.LogInformation("Processing {Job} (ForceRefresh: {ForceRefresh})", nameof(CheckTraktTokenJob), ForceRefresh);
            var settings = _settingsProvider.GetSettings();
            if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.TokenExpirationDate))
            {
                _logger.LogInformation("Trakt is not enabled or no token expiration date is set");
                return Task.CompletedTask;
            }

            // Convert the Unix timestamp to DateTime
            var expirationDate = DateTimeOffset.FromUnixTimeSeconds(long.Parse(settings.TraktTv.TokenExpirationDate)).DateTime;

            // Check if the token needs refreshing
            if (ForceRefresh || DateTime.Now.Add(TimeSpan.FromDays(45)) >= expirationDate)
            {
                if (_traktHelper.RefreshAuthToken())
                {
                    var newExpirationDate = DateTimeOffset.FromUnixTimeSeconds(long.Parse(settings.TraktTv.TokenExpirationDate)).DateTime;
                    _logger.LogInformation("Trakt token refreshed successfully. New expiry date: {Date}", newExpirationDate);
                }
            }
            else
            {
                _logger.LogInformation("Trakt token is still valid. Expiry date: {Date}", expirationDate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Job}: {Ex}", nameof(CheckTraktTokenJob), ex);
        }

        return Task.CompletedTask;
    }

    public CheckTraktTokenJob(ISettingsProvider settingsProvider, TraktTVHelper traktHelper)
    {
        _settingsProvider = settingsProvider;
        _traktHelper = traktHelper;
    }

    protected CheckTraktTokenJob() { }
}
