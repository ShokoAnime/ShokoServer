using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
///   Dispatched once per entry and write when the stored texts of the entry
///   changed, whoever wrote them.
/// </summary>
public class EntityTextsChangedEventArgs : EventArgs
{
    /// <summary>
    ///   The entry whose texts changed.
    /// </summary>
    public required MetadataGuid EntityID { get; init; }

    /// <summary>
    ///   Which kinds of text changed.
    /// </summary>
    public required IReadOnlySet<TextKind> Kinds { get; init; }

    /// <summary>
    ///   The sources whose texts changed.
    /// </summary>
    public required IReadOnlySet<MetadataSource> Sources { get; init; }
}
