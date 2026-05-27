using System;

namespace Shoko.QueueProcessor.Concurrency;

/// <summary>
/// Per-type override for the retry backoff policy. When not present, global defaults from
/// <see cref="QueueProcessorOptions"/> apply.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class RetryPolicyAttribute : Attribute
{
    /// <summary>Maximum number of retry attempts before the job is discarded with an ERROR log.</summary>
    public int MaxRetries { get; init; } = -1;  // -1 = use global default

    /// <summary>Base delay in seconds for the first retry. Subsequent retries are <c>BaseDelay * 2^n</c>.</summary>
    public int BaseDelaySeconds { get; init; } = -1;  // -1 = use global default

    /// <summary>Maximum delay cap in seconds. The backoff will not exceed this value.</summary>
    public int MaxDelaySeconds { get; init; } = -1;  // -1 = use global default
}
