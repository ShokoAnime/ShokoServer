using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
///   Dispatched once per write that changed links, whoever wrote them, with
///   every link it changed.
/// </summary>
public class MetadataLinksChangedEventArgs : EventArgs
{
    /// <summary>
    ///   Why the links were written.
    /// </summary>
    public required MetadataLinkChangeReason Reason { get; init; }

    /// <summary>
    ///   The links that changed, never empty.
    /// </summary>
    public required IReadOnlyList<MetadataLinkChange> Changes { get; init; }

    /// <summary>
    ///   The sources the changed links point at.
    /// </summary>
    public IReadOnlySet<MetadataSource> Sources => Changes.Select(change => change.Source).ToHashSet();

    /// <summary>
    ///   The AniDB anime the changed links belong to.
    /// </summary>
    public IReadOnlySet<int> AnidbAnimeIDs => Changes.Select(change => change.AnidbAnimeID).ToHashSet();

    /// <summary>
    ///   The API token of whoever changed the links, or <c>null</c>
    ///   when the system did it. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
