using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Shoko.Server.Utilities;

/// <summary>
///   A shared pool for the short strings that repeat across the rows held in
///   memory, so equal values share one instance.
/// </summary>
/// <remarks>
///   A fixed table of slots, one value each, written without locks. A value
///   whose slot is taken by another replaces it, so the pool never grows and
///   holds nothing the rows have let go of for long.
/// </remarks>
public static class StringPool
{
    #region Fields

    /// <summary>
    ///   The longest value pooled. Longer values rarely repeat.
    /// </summary>
    internal const int MaxLength = 64;

    /// <summary>
    ///   How many slots the pool has, a power of two.
    /// </summary>
    private const int SlotCount = 1 << 17;

    private static readonly string?[] _slots = new string?[SlotCount];

    #endregion

    #region Methods

    /// <summary>
    ///   Gets the pooled instance equal to a value, pooling the value when no
    ///   equal instance is.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>An instance equal to <paramref name="value"/>, or <c>null</c> when it is <c>null</c>.</returns>
    [return: NotNullIfNotNull(nameof(value))]
    public static string? Get(string? value)
    {
        if (value is null)
            return null;
        if (value.Length is 0)
            return string.Empty;
        if (value.Length > MaxLength)
            return value;

        ref var slot = ref _slots[value.GetHashCode() & (SlotCount - 1)];
        var pooled = Volatile.Read(ref slot);
        if (pooled is not null && string.Equals(pooled, value))
            return pooled;

        Volatile.Write(ref slot, value);
        return value;
    }

    /// <summary>
    ///   Gets the pooled instance equal to a run of characters, pooling a new
    ///   string made of them when no equal instance is.
    /// </summary>
    /// <param name="value">The characters.</param>
    /// <returns>An instance equal to <paramref name="value"/>.</returns>
    public static string Get(ReadOnlySpan<char> value)
    {
        if (value.Length is 0)
            return string.Empty;
        if (value.Length > MaxLength)
            return value.ToString();

        // Hashes the same as the string would, so both overloads share slots.
        ref var slot = ref _slots[string.GetHashCode(value) & (SlotCount - 1)];
        var pooled = Volatile.Read(ref slot);
        if (pooled is not null && value.SequenceEqual(pooled))
            return pooled;

        var created = value.ToString();
        Volatile.Write(ref slot, created);
        return created;
    }

    #endregion
}
