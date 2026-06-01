using System;
using System.Collections.Generic;

namespace Shoko.QueueProcessor.Chain;

/// <summary>
/// Thrown from a job to intentionally abort the rest of the chain.
/// Non-finally descendants are skipped; jobs marked <see cref="ChainFinallyAttribute"/> still run.
/// Supports multiple inner exceptions for aggregate failure reporting.
/// </summary>
public class ChainAbortException : Exception
{
    public IReadOnlyList<Exception> InnerExceptions { get; }

    public ChainAbortException() : base("Job chain aborted.") { InnerExceptions = []; }

    public ChainAbortException(string message) : base(message) { InnerExceptions = []; }

    public ChainAbortException(string message, Exception inner)
        : base(message, inner) { InnerExceptions = [inner]; }

    public ChainAbortException(string message, IReadOnlyList<Exception> innerExceptions)
        : base(message, innerExceptions.Count > 0 ? innerExceptions[0] : null)
    {
        InnerExceptions = innerExceptions;
    }
}
