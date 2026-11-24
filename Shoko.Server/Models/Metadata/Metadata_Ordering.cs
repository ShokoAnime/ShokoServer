using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   A stored ordering of a series: a plugin's global one, or a user's own
///   under the <c>user</c> source. Its titles and overviews are in the text
///   store, under its <see cref="ID"/>.
/// </summary>
public class Metadata_Ordering : IMetadataStoreRow<Metadata_Ordering>
{
    #region Database Columns

    /// <summary>
    ///   The row's ID.
    /// </summary>
    public int Metadata_OrderingID { get; set; }

    /// <summary>
    ///   The source the ordering is stored under.
    /// </summary>
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    ///   The source's own ID for the ordering.
    /// </summary>
    public string ProviderID { get; set; } = string.Empty;

    /// <summary>
    ///   The source of the series the ordering orders.
    /// </summary>
    public MetadataSource SeriesSource { get; set; } = null!;

    /// <summary>
    ///   The series' own ID on its source.
    /// </summary>
    public string SeriesID { get; set; } = string.Empty;

    /// <summary>
    ///   What the ordering follows.
    /// </summary>
    public OrderingType Type { get; set; }

    /// <summary>
    ///   Whether the source marks the ordering as the series' default
    ///   everywhere.
    /// </summary>
    public bool IsSourceDefault { get; set; }

    /// <summary>
    ///   The countries, as upper-case ISO 3166-1 alpha-2 codes, where the
    ///   source marks the ordering as the series' default.
    /// </summary>
    public List<string> SourceDefaultCountries { get; set; } = [];

    /// <summary>
    ///   What the ordering's absolute episode numbers are.
    /// </summary>
    public AbsoluteEpisodeNumbering AbsoluteNumbering { get; set; }

    /// <summary>
    ///   When the ordering was first stored.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///   When the ordering last changed.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Helpers

    /// <summary>
    ///   The ordering's identifier.
    /// </summary>
    public MetadataGuid ID => new(Source, MetadataEntityType.Ordering, ProviderID);

    /// <summary>
    ///   The ordered series' identifier.
    /// </summary>
    public MetadataGuid SeriesGuid => new(SeriesSource, MetadataEntityType.Series, SeriesID);

    /// <summary>
    ///   Whether the stored columns are the same as another row's, leaving
    ///   out the row's ID and dates.
    /// </summary>
    /// <param name="other">The other row.</param>
    /// <returns><c>true</c> when nothing but the ID and dates differ.</returns>
    internal bool SameAs(Metadata_Ordering other)
        => Source == other.Source &&
            ProviderID == other.ProviderID &&
            SeriesSource == other.SeriesSource &&
            SeriesID == other.SeriesID &&
            Type == other.Type &&
            IsSourceDefault == other.IsSourceDefault &&
            SourceDefaultCountries.SequenceEqual(other.SourceDefaultCountries) &&
            AbsoluteNumbering == other.AbsoluteNumbering;

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Ordering>.RowID
    {
        get => Metadata_OrderingID;
        set => Metadata_OrderingID = value;
    }

    Metadata_Ordering IMetadataStoreRow<Metadata_Ordering>.Clone()
        => (Metadata_Ordering)MemberwiseClone();

    #endregion
}
