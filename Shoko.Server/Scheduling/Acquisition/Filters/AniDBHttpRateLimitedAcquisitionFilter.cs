using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Anidb.Events;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Utilities;

namespace Shoko.Server.Scheduling.Acquisition.Filters;

public class AniDBHttpRateLimitedAcquisitionFilter : IAcquisitionFilter
{
    private readonly Type[] _types;
    private readonly AniDbBanState _banState;

    public AniDBHttpRateLimitedAcquisitionFilter(AniDbBanStateService banStateService)
    {
        _banState = banStateService.Http;
        _banState.BanOccurred += OnBanChanged;
        _banState.BanExpired += OnBanChanged;
        _types = ReflectionUtils.ScannableAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(a => typeof(IQueueJob).IsAssignableFrom(a) && !a.IsAbstract &&
                        a.GetCustomAttributes(inherit: true).OfType<AniDBHttpRateLimitedAttribute>().Any())
            .ToArray();
    }

    ~AniDBHttpRateLimitedAcquisitionFilter()
    {
        _banState.BanOccurred -= OnBanChanged;
        _banState.BanExpired -= OnBanChanged;
    }

    public Type? WatchedAttributeType => typeof(AniDBHttpRateLimitedAttribute);

    private void OnBanChanged(object? sender, AnidbBanOccurredEventArgs e) => StateChanged?.Invoke(null, EventArgs.Empty);

    // Network availability is handled by NetworkRequiredAcquisitionFilter.
    // HTTP does not require login (unlike UDP), so IsAlive / session state is irrelevant here.
    // Only block when actively banned.
    public IEnumerable<Type> GetTypesToExclude() =>
        _banState.IsBanned ? _types : [];

    public event EventHandler? StateChanged;
}
