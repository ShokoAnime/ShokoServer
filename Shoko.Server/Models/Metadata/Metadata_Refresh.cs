using System;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   When the core last had a plugin source's series, movie or collection
///   refreshed by its provider, which is what the refresh job's staleness
///   check reads.
/// </summary>
public class Metadata_Refresh
{
    #region Database Columns

    /// <summary>
    ///   The row's ID.
    /// </summary>
    public int Metadata_RefreshID { get; set; }

    /// <summary>
    ///   The source the entry belongs to.
    /// </summary>
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    ///   Whether the entry is a series, a movie or a collection.
    /// </summary>
    public MetadataEntityType EntityType { get; set; } = null!;

    /// <summary>
    ///   The source's own ID for the entry.
    /// </summary>
    public string ProviderID { get; set; } = string.Empty;

    /// <summary>
    ///   When the provider last refreshed the entry without failing.
    /// </summary>
    public DateTime LastRefreshedAt { get; set; }

    #endregion

    #region Helpers

    /// <summary>
    ///   The entry's identifier.
    /// </summary>
    public MetadataGuid EntryID => new(Source, EntityType, ProviderID);

    #endregion
}
