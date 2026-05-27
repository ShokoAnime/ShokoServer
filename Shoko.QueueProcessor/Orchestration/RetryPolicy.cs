#nullable enable
using System;
using System.Reflection;
using Shoko.QueueProcessor.Concurrency;

namespace Shoko.QueueProcessor.Orchestration;

/// <summary>Exponential-backoff retry policy for a job type.</summary>
public class RetryPolicy
{
    public int MaxRetries { get; init; } = 8;
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Returns the delay before attempt number <paramref name="retryCount"/>
    /// (0-based: retryCount=0 → first retry).
    /// </summary>
    public TimeSpan GetDelay(int retryCount)
    {
        var seconds = Math.Min(
            BaseDelay.TotalSeconds * Math.Pow(2, retryCount),
            MaxDelay.TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>True when the job should be discarded rather than retried again.</summary>
    public bool ShouldDiscard(int retryCount) => retryCount >= MaxRetries;
}

/// <summary>
/// Resolves the effective <see cref="RetryPolicy"/> for a given job type,
/// applying per-type attribute overrides where present.
/// </summary>
public class RetryPolicyResolver
{
    private readonly RetryPolicy _global;

    public RetryPolicyResolver(RetryPolicy global)
    {
        _global = global;
    }

    /// <summary>Returns the retry policy applicable to <paramref name="jobType"/>.</summary>
    public RetryPolicy For(Type jobType)
    {
        var attr = jobType.GetCustomAttribute<RetryPolicyAttribute>();
        if (attr == null) return _global;

        return new RetryPolicy
        {
            MaxRetries = attr.MaxRetries >= 0 ? attr.MaxRetries : _global.MaxRetries,
            BaseDelay = attr.BaseDelaySeconds >= 0
                ? TimeSpan.FromSeconds(attr.BaseDelaySeconds)
                : _global.BaseDelay,
            MaxDelay = attr.MaxDelaySeconds >= 0
                ? TimeSpan.FromSeconds(attr.MaxDelaySeconds)
                : _global.MaxDelay
        };
    }
}
