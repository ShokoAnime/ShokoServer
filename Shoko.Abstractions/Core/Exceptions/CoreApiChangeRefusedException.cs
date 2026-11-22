using System;
using System.Collections.Generic;
using Shoko.Abstractions.Web;

namespace Shoko.Abstractions.Core.Exceptions;

/// <summary>
///   Thrown when core APIs cannot be turned on or off, saying which and why.
/// </summary>
/// <param name="message">What was refused, for people.</param>
/// <param name="apis">The APIs that could not change.</param>
/// <param name="reason">Why.</param>
/// <param name="webUIs">The web UIs in use that need them, when that is why.</param>
public class CoreApiChangeRefusedException(string message, CoreApis apis, CoreApiChangeRefusal reason, IReadOnlyList<WebUIProviderInfo>? webUIs = null)
    : InvalidOperationException(message)
{
    /// <summary>
    ///   The APIs that could not change.
    /// </summary>
    public CoreApis Apis { get; } = apis;

    /// <summary>
    ///   Why they could not change.
    /// </summary>
    public CoreApiChangeRefusal Reason { get; } = reason;

    /// <summary>
    ///   The web UIs in use that need the APIs, when
    ///   <see cref="Reason"/> is <see cref="CoreApiChangeRefusal.NeededByWebUI"/>;
    ///   otherwise empty.
    /// </summary>
    public IReadOnlyList<WebUIProviderInfo> WebUIs { get; } = webUIs ?? [];
}
