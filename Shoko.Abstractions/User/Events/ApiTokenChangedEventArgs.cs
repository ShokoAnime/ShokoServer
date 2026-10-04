using System;

namespace Shoko.Abstractions.User.Events;

/// <summary>
///   Dispatched when an API token is generated or invalidated.
/// </summary>
public class ApiTokenChangedEventArgs : EventArgs
{
    /// <summary>
    ///   The token that was generated or invalidated, with the user it belongs
    ///   to and the device it was registered to. Its
    ///   <see cref="ApiToken.Token"/> is left out whenever it is serialized.
    /// </summary>
    public required ApiToken ApiToken { get; init; }

    /// <summary>
    ///   When the event occurred.
    /// </summary>
    public required DateTime OccurredAt { get; init; }

    /// <summary>
    ///   The API token of whoever generated or invalidated the token, or
    ///   <c>null</c> when the system did it. Stamped when the event is
    ///   raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
