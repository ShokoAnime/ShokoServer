using System;

namespace Shoko.QueueProcessor.Concurrency;

/// <summary>
/// Marks a job as expected to run for an extended period, exempting it from the watchdog timeout.
/// Apply to jobs that are inherently long-running (e.g., hashing large files, AVDump, full-library
/// scans) where a warning after the default <see cref="QueueProcessorOptions.WatchdogTimeoutSeconds"/>
/// would be a false positive.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class LongRunningAttribute : Attribute { }
