using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Services.MetadataStorage;

namespace Shoko.Server.Models.CrossReference;

/// <summary>
/// What every stored metadata link carries, whichever level it is made at.
/// Each level has its own table, so the level is the row's type rather than a
/// column.
/// </summary>
public abstract class CrossRef_AniDB_Metadata : IMetadataCrossReference
{
    #region Database Columns

    /// <summary>
    /// The source the linked entry belongs to.
    /// </summary>
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    /// The AniDB anime, which every link belongs to whatever its level.
    /// </summary>
    public int AnidbAnimeID { get; set; }

    /// <summary>
    /// The provider's own ID. Empty marks an entry deliberately linked to
    /// nothing, so matching leaves it alone.
    /// </summary>
    public string ProviderID { get; set; } = string.Empty;

    /// <summary>
    /// How the link was arrived at.
    /// </summary>
    public MatchRating MatchRating { get; set; }

    /// <summary>
    /// Where this link sits when an entry carries several.
    /// </summary>
    public int Ordering { get; set; }

    /// <summary>
    /// The provider that wrote the link, or nothing when the core's own did.
    /// </summary>
    public Guid? WrittenBy { get; set; }

    #endregion

    #region Cross-Reference Implementation

    /// <inheritdoc />
    public abstract MetadataEntityType EntityType { get; }

    /// <summary>
    /// The kind of entry <see cref="ProviderID"/> names, which is the level's
    /// own kind but where the level lets a link name another.
    /// </summary>
    public virtual MetadataEntityType ProviderEntityType => EntityType;

    /// <summary>
    /// Whether the link says the entry is deliberately on no such source.
    /// </summary>
    public bool IsUnlinked => string.IsNullOrWhiteSpace(ProviderID);

    /// <inheritdoc />
    MetadataGuid? IMetadataCrossReference.ProviderID => ToProviderID(Source, ProviderEntityType, ProviderID);

    /// <inheritdoc />
    public IShokoSeries? ShokoSeries => RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    /// <inheritdoc />
    /// <remarks>
    ///   Resolved by the core, whether or not the source's provider is
    ///   enabled, so a link reads the same whoever may currently write it.
    /// </remarks>
    public IMetadata? Provider
        => IsUnlinked ? null : MetadataEntries.Resolve(Source, ProviderEntityType, ProviderID);

    #endregion

    #region Conversion

    /// <summary>
    /// Reads a stored provider ID back as the entry it names.
    /// </summary>
    /// <param name="source">The source the entry is on.</param>
    /// <param name="entityType">The kind of entry it is.</param>
    /// <param name="providerID">The ID as stored.</param>
    /// <returns>
    /// The entry, or <c>null</c> when the ID is empty or blank, which is how
    /// no entry is kept at rest.
    /// </returns>
    internal static MetadataGuid? ToProviderID(MetadataSource source, MetadataEntityType entityType, string? providerID)
        => string.IsNullOrWhiteSpace(providerID) ? null : new(source, entityType, providerID);

    /// <summary>
    /// Whether the link names an entry: the same source ID of the same kind,
    /// or nothing for a link deliberately made to nothing.
    /// </summary>
    /// <remarks>
    /// The kind matters at the series' level, where a series and a film
    /// claiming the whole anime may share a raw ID.
    /// </remarks>
    /// <param name="providerID">The entry, or <c>null</c> for no entry.</param>
    /// <returns><c>true</c> when the link names it.</returns>
    internal bool Names(MetadataGuid? providerID)
        => providerID is null
            ? IsUnlinked
            : !IsUnlinked && ProviderEntityType == providerID.EntityType && string.Equals(ProviderID, providerID.ID, StringComparison.Ordinal);

    /// <summary>
    /// Writes an entry's ID the way it is kept at rest.
    /// </summary>
    /// <param name="providerID">The entry, or <c>null</c> for no entry.</param>
    /// <returns>The source's own ID, or an empty string for no entry.</returns>
    internal static string ToStored(MetadataGuid? providerID)
        => providerID?.ID ?? string.Empty;

    #endregion

    #region Storage

    /// <summary>
    /// The stored row's ID, which is 0 until it has been saved.
    /// </summary>
    internal abstract int RowID { get; }

    /// <summary>
    /// The entry this link's position is counted within. A concept of the
    /// store, not a claim the link makes: at the series' level nothing stands
    /// in the episode's place.
    /// </summary>
    internal abstract (MetadataSource Source, int AnidbAnimeID, int AnidbEpisodeID) Slot { get; }

    #endregion
}
