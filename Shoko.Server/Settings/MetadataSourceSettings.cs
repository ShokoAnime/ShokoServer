using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;

namespace Shoko.Server.Settings;

/// <summary>
/// What has been decided about one source.
/// </summary>
public class MetadataSourceSettings
{
    /// <summary>
    /// The providers claiming each entity type, in the order they are tried:
    /// the first enabled one still registered answers, and the rest stand by.
    /// Missing is undecided; none enabled is decided as nobody.
    /// </summary>
    public Dictionary<MetadataEntityType, List<MetadataProviderAssignment>> Providers { get; set; } = [];

    /// <summary>
    /// The provider answering for each entity type, as older settings kept it.
    /// Read into <see cref="Providers"/> on start and never written back.
    /// <see langword="null"/> is decided as nobody.
    /// </summary>
    public Dictionary<MetadataEntityType, Guid?>? Enabled { get; set; }

    /// <summary>
    /// Keeps <see cref="Enabled"/> out of the saved settings.
    /// </summary>
    /// <returns>Always <see langword="false"/>.</returns>
    public bool ShouldSerializeEnabled()
        => false;

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
