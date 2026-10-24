using System;
using Shoko.Abstractions.User.Events;

namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// Dispatched when an API token is created or removed. The key itself is
/// never sent.
/// </summary>
/// <param name="args">The event arguments.</param>
public class ApiTokenChangedSignalRModel(ApiTokenChangedEventArgs args)
{
    /// <summary>
    /// The ID of the user the token is for.
    /// </summary>
    public int UserID { get; } = args.ApiToken.User.LocalID;

    /// <summary>
    /// The device the token is for.
    /// </summary>
    public string Device { get; } = args.ApiToken.Device;

    /// <summary>
    /// When the token expires, or <c>null</c> when it never does.
    /// </summary>
    public DateTime? ExpiresAt { get; } = args.ApiToken.ExpiresAt?.ToUniversalTime();

    /// <summary>
    /// When the token was created or removed, in UTC.
    /// </summary>
    public DateTime OccurredAt { get; } = args.OccurredAt;
}
