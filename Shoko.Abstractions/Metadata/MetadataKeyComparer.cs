using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   Compares the keys a <see cref="MetadataSource"/> or a
///   <see cref="MetadataEntityType"/> is looked up by, its value and its
///   aliases: ignoring case, and reading <c>_</c> as <c>-</c>.
/// </summary>
internal sealed class MetadataKeyComparer : IEqualityComparer<string>
{
    #region Instance

    private MetadataKeyComparer() { }

    /// <summary>
    ///   The shared instance.
    /// </summary>
    public static MetadataKeyComparer Instance { get; } = new();

    #endregion

    #region Comparison

    /// <summary>
    ///   Reads every <c>_</c> in a key as <c>-</c>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The key with hyphens for underscores, or the same instance if it had none.</returns>
    public static string Normalize(string key)
        => key.Replace('_', '-');

    /// <summary>
    ///   Checks whether two keys are the same, ignoring case and reading
    ///   <c>_</c> as <c>-</c>.
    /// </summary>
    /// <param name="x">The first key.</param>
    /// <param name="y">The second key.</param>
    /// <returns><c>true</c> if both are <c>null</c> or name the same key.</returns>
    public bool Equals(string? x, string? y)
        => x is null || y is null ? x is null && y is null : StringComparer.OrdinalIgnoreCase.Equals(Normalize(x), Normalize(y));

    /// <summary>
    ///   Gets a hash code that is the same for keys <see cref="Equals(string?, string?)"/>
    ///   finds the same.
    /// </summary>
    /// <param name="obj">The key.</param>
    /// <returns>The hash code.</returns>
    public int GetHashCode([DisallowNull] string obj)
        => StringComparer.OrdinalIgnoreCase.GetHashCode(Normalize(obj));

    #endregion
}
