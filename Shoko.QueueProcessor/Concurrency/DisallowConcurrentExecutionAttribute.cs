using System;

namespace Shoko.QueueProcessor.Concurrency;

/// <summary>
/// Shorthand for <c>[LimitConcurrency(1)]</c> applied at the group level.
/// Only one job of this type (or its group) may execute at a time.
/// Equivalent to Quartz's <c>[DisallowConcurrentExecution]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DisallowConcurrentExecutionAttribute : Attribute { }
