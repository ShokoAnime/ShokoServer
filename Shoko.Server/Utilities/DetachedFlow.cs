using System;
using System.Threading;

namespace Shoko.Server.Utilities;

/// <summary>
///   Starts long-lived work, such as a timer, a loop or a dedicated thread,
///   without the ambient state of whoever started it. A timer created inside a
///   request would otherwise keep that request's actor, and any other async
///   local, for every tick.
/// </summary>
public static class DetachedFlow
{
    /// <summary>
    ///   Stops the execution context from flowing into timers, threads and
    ///   tasks started on this thread until the returned scope is disposed.
    /// </summary>
    /// <remarks>
    ///   The scope must be disposed on the thread that took it, so it cannot
    ///   span an <c>await</c>.
    /// </remarks>
    /// <returns>
    ///   The scope, which does nothing when the flow is suppressed already.
    /// </returns>
    public static IDisposable Suppress()
        => ExecutionContext.IsFlowSuppressed() ? NoScope.Instance : ExecutionContext.SuppressFlow();

    /// <summary>
    ///   A scope that does nothing.
    /// </summary>
    private sealed class NoScope : IDisposable
    {
        /// <summary>
        ///   The scope.
        /// </summary>
        public static readonly NoScope Instance = new();

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
