using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Utilities;

/// <summary>
///   A shared pool for the metadata IDs that repeat across the rows held in
///   memory, such as the AniDB anime every release of a series names, so equal
///   IDs share one instance.
/// </summary>
/// <remarks>
///   A fixed table of slots, one ID each, written without locks, as
///   <see cref="StringPool"/> is. An ID whose slot is taken by another
///   replaces it, so the pool never grows.
/// </remarks>
public static class MetadataGuidPool
{
    #region Fields

    /// <summary>
    ///   How many slots the pool has, a power of two.
    /// </summary>
    private const int SlotCount = 1 << 16;

    private static readonly MetadataGuid?[] _slots = new MetadataGuid?[SlotCount];

    #endregion

    #region Methods

    /// <summary>
    ///   Gets the pooled instance equal to an ID, pooling the ID when no equal
    ///   instance is.
    /// </summary>
    /// <param name="value">The ID.</param>
    /// <returns>An instance equal to <paramref name="value"/>, or <c>null</c> when it is <c>null</c>.</returns>
    [return: NotNullIfNotNull(nameof(value))]
    public static MetadataGuid? Get(MetadataGuid? value)
    {
        if (value is null)
            return null;

        ref var slot = ref _slots[value.GetHashCode() & (SlotCount - 1)];
        var pooled = Volatile.Read(ref slot);
        if (pooled is not null && pooled.Equals(value))
            return pooled;

        Volatile.Write(ref slot, value);
        return value;
    }

    #endregion
}
