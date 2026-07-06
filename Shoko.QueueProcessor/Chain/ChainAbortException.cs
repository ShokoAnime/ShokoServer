using System;

namespace Shoko.QueueProcessor.Chain;

/// <summary>
/// Thrown from a job to intentionally abort the rest of the chain.
/// Non-finally descendants are skipped; jobs marked <see cref="ChainFinallyAttribute"/> still run.
/// Supports multiple inner exceptions for aggregate failure reporting.
/// </summary>
public class ChainAbortException : Exception
{
    public ChainAbortException() : base("Job chain aborted.") { }

    public ChainAbortException(string message) : base(message) { }

    public ChainAbortException(string message, Exception inner) : base(message, inner) { }

    public ChainAbortException(Exception inner) : base("Job chain aborted.", inner) { }
}
