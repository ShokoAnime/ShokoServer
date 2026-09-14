using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Providers.Anilist;
using Shoko.Server.Scheduling.Acquisition.Attributes;

#nullable enable
namespace Shoko.Server.Scheduling.Acquisition.Filters;

public class AnilistApiRateLimitedAcquisitionFilter : IAcquisitionFilter, IDisposable
{
    private readonly Type[] _types;

    private readonly AnilistRateLimiter _rateLimiter;

    public AnilistApiRateLimitedAcquisitionFilter(AnilistRateLimiter rateLimiter)
    {
        _rateLimiter = rateLimiter;
        _rateLimiter.PauseStateChanged += OnPauseStateChanged;
        _types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(a => typeof(IQueueJob).IsAssignableFrom(a) && !a.IsAbstract &&
                        a.GetCustomAttributes(inherit: true).OfType<AnilistApiRateLimitedAttribute>().Any())
            .ToArray();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
            _rateLimiter.PauseStateChanged -= OnPauseStateChanged;
    }

    public Type? WatchedAttributeType => typeof(AnilistApiRateLimitedAttribute);

    private void OnPauseStateChanged(object? sender, EventArgs e) => StateChanged?.Invoke(this, EventArgs.Empty);

    // Network availability is handled by NetworkRequiredAcquisitionFilter.
    // Only block when the 5XX circuit breaker is tripped — 429 and quota backoff is handled inside EnsureRateAsync.
    public IEnumerable<Type> GetTypesToExclude() =>
        _rateLimiter.Is5xxPaused ? _types : [];

    public event EventHandler? StateChanged;
}
