using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Settings;

/// <summary>
/// What has been decided about one source.
/// </summary>
public class MetadataSourceSettings
{
    /// <summary>
    /// The provider answering for each entity type. Missing is undecided;
    /// <see langword="null"/> is decided as nobody.
    /// </summary>
    public Dictionary<MetadataEntityType, Guid?> Enabled { get; set; } = [];

    /// <summary>
    /// The provider that works out what an anime is, when asked or on its own.
    /// </summary>
    public Guid? AutoLinker { get; set; }

    /// <summary>
    /// Whether no provider able to auto-link has been seen for this source
    /// yet, so the first one that is claims <see cref="AutoLinker"/>. Cleared
    /// by that claim or by an admin choosing, so an entry without it is decided.
    /// </summary>
    public bool AutoLinkerUnclaimed { get; set; }

    /// <summary>
    /// Whether <see cref="AutoLinker"/> also does it on its own.
    /// </summary>
    public bool AutoLink { get; set; }

    /// <summary>
    /// Whether <see cref="AutoLinker"/> may link restricted entries.
    /// </summary>
    public bool AutoLinkRestricted { get; set; }
}
