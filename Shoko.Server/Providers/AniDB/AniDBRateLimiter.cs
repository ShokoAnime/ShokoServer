using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Server.Settings;

using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

#nullable enable
namespace Shoko.Server.Providers.AniDB;

public abstract class AniDBRateLimiter
{
    private readonly ILogger _logger;
    private readonly object _lock = new();

    private readonly object _settingsLock = new();

    private readonly Stopwatch _requestWatch = new();

    private readonly Stopwatch _activeTimeWatch = new();

    private readonly ISettingsProvider _settingsProvider;

    private readonly IShokoEventHandler _eventHandler;

    private readonly Func<IServerSettings, AnidbRateLimitSettings> _settingsSelector;

    private int? _shortDelay = null;

    // From AniDB's wiki about UDP rate limiting:
    // Short Term:
    // A Client MUST NOT send more than 0.5 packets per second(that's one packet every two seconds, not two packets a second!)
    // The server will start to enforce the limit after the first 5 packets have been received.
    private int ShortDelay
    {
        get
        {
            EnsureUsable();

            return _shortDelay!.Value;
        }
    }

    private int? _longDelay = null;

    // From AniDB's wiki about UDP rate limiting:
    // Long Term:
    // A Client MUST NOT send more than one packet every four seconds over an extended amount of time.
    // An extended amount of time is not defined. Use common sense.
    private int LongDelay
    {
        get
        {
            EnsureUsable();

            return _longDelay!.Value;
        }
    }

    private long? _shortPeriod = null;

    // Switch to longer delay after a short period
    private long ShortPeriod
    {
        get
        {
            EnsureUsable();

            return _shortPeriod!.Value;
        }
    }

    private long? _resetPeriod = null;

    // Switch to shorter delay after inactivity
    private long ResetPeriod
    {
        get
        {
            EnsureUsable();

            return _resetPeriod!.Value;
        }
    }

    /// <summary>
    /// Ensures that all the rate limiting values are usable.
    /// </summary>
    /// <param name="force">Force the values to be reapplied from settings, even if they are already in a usable state.</param>
    private void EnsureUsable(bool force = false)
    {
        if (!force && _shortDelay.HasValue)
            return;

        lock (_settingsLock)
        {
            if (!force && _shortDelay.HasValue)
                return;

            var settings = _settingsSelector(_settingsProvider.GetSettings());
            var baseRate = settings.BaseRateInSeconds * 1000;
            _shortDelay = baseRate;
            _longDelay = baseRate * settings.SlowRateMultiplier;
            _shortPeriod = baseRate * settings.SlowRatePeriodMultiplier;
            _resetPeriod = baseRate * settings.ResetPeriodMultiplier;
        }
    }

    protected AniDBRateLimiter(ILogger logger, ISettingsProvider settingsProvider, IShokoEventHandler eventHandler, Func<IServerSettings, AnidbRateLimitSettings> settingsSelector)
    {
        _logger = logger;
        _requestWatch.Start();
        _activeTimeWatch.Start();
        _settingsProvider = settingsProvider;
        _settingsSelector = settingsSelector;
        _eventHandler = eventHandler;
        _eventHandler.SettingsSaved += OnSettingsSaved;
    }

    ~AniDBRateLimiter()
    {
        _eventHandler.SettingsSaved -= OnSettingsSaved;
    }

    private void OnSettingsSaved(object? sender, SettingsSavedEventArgs eventArgs)
    {
        // Reset the cached values when the settings are updated.
        EnsureUsable(true);
    }

    private void ResetRate()
    {
        var elapsedTime = _activeTimeWatch.ElapsedMilliseconds;
        _activeTimeWatch.Restart();
        _logger.LogTrace("Rate is reset. Active time was {Time} ms", elapsedTime);
    }

    public T EnsureRate<T>(Func<T> action, bool forceShortDelay = false)
    {
        lock (_lock)
        try
        {
            var delay = _requestWatch.ElapsedMilliseconds;
            if (delay > ResetPeriod) ResetRate();
            var currentDelay = !forceShortDelay && _activeTimeWatch.ElapsedMilliseconds > ShortPeriod ? LongDelay : ShortDelay;

            if (delay > currentDelay)
            {
                _logger.LogTrace("Time since last request is {Delay} ms, not throttling", delay);
                _logger.LogTrace("Sending AniDB command");
                return action();
            }

            // add 50ms for good measure
            var waitTime = currentDelay - (int)delay + 50;

            _logger.LogTrace("Time since last request is {Delay} ms, throttling for {Time}ms", delay, waitTime);
            Thread.Sleep(waitTime);

            _logger.LogTrace("Sending AniDB command");
            return action();
        }
        finally
        {
            _requestWatch.Restart();
        }
    }
}
