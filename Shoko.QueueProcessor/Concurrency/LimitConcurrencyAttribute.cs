using System;

namespace Shoko.QueueProcessor.Concurrency;

/// <summary>
/// Limits how many workers can simultaneously execute jobs of this type (or its concurrency group).
/// <para>
/// When combined with <see cref="DisallowConcurrencyGroupAttribute"/>, the limit applies to the
/// entire group, not just this type.  The pool created for the group will have
/// <see cref="MaxConcurrentJobs"/> worker threads.
/// </para>
/// <para>
/// When used alone (no group), a dedicated single-type pool of <see cref="MaxConcurrentJobs"/>
/// workers is created for this type.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class LimitConcurrencyAttribute : Attribute
{
    /// <summary>Default maximum concurrent workers for this type / group.</summary>
    public int MaxConcurrentJobs { get; }

    /// <summary>
    /// Hard upper bound. <see cref="QueueProcessorOptions.LimitedConcurrencyOverrides"/> can lower
    /// the limit but never exceed this value. <c>0</c> means no hard cap (override can go up to
    /// <see cref="QueueProcessorOptions.MaxTotalWorkers"/>).
    /// </summary>
    public int MaxAllowedConcurrentJobs { get; }

    /// <param name="maxConcurrentJobs">Maximum concurrent workers.</param>
    /// <param name="maxAllowedConcurrentJobs">
    /// Hard upper cap for runtime overrides. Defaults to <paramref name="maxConcurrentJobs"/>.
    /// </param>
    public LimitConcurrencyAttribute(int maxConcurrentJobs, int maxAllowedConcurrentJobs = 0)
    {
        MaxConcurrentJobs = maxConcurrentJobs;
        MaxAllowedConcurrentJobs = maxAllowedConcurrentJobs == 0 ? maxConcurrentJobs : maxAllowedConcurrentJobs;
    }
}
