using System;
using System.Threading;

#nullable enable
namespace Shoko.Server.Utilities;

/// <summary>
/// An <see cref="IDisposable"/> that runs one action when it is disposed, for
/// the handles that exist only to undo something: releasing a keyed lock,
/// removing a subscription, restoring a swapped-out static.
/// </summary>
/// <remarks>
/// Only <see cref="IDisposable"/> ever crosses into the abstractions, so a
/// plugin holding one of these handles never sees this type — it is the
/// server's own way of building one without a bespoke class each time.
/// </remarks>
/// <param name="action">The action to run on disposal. It runs at most once.</param>
public sealed class DisposableAction(Action action) : IDisposable
{
    private readonly Action _action = action ?? throw new ArgumentNullException(nameof(action));

    private int _disposed;

    /// <summary>
    /// Runs the action, once. Disposing again, from any thread, does nothing.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
            return;

        _action();
    }
}
