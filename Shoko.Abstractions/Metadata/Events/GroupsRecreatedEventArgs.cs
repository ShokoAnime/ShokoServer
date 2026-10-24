using System;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
///   Dispatched when all groups have been recreated.
/// </summary>
public class GroupsRecreatedEventArgs : EventArgs
{
    /// <summary>
    ///   The API token of whoever had the groups recreated, or <c>null</c>
    ///   when the system did it. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
