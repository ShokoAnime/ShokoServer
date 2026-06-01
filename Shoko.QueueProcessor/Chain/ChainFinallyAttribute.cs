using System;

namespace Shoko.QueueProcessor.Chain;

/// <summary>
/// Marks a job as a "finally" step in a chain: it runs even when the chain is aborted via
/// <see cref="ChainAbortException"/>. Its descendants also run normally after it completes.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class ChainFinallyAttribute : Attribute { }
