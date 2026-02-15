using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Shoko.Abstractions.Extensions;

/// <summary>
/// Extension methods for IEnumerable.
/// </summary>
public static class EnumerableExtensions
{
    /// <summary>
    /// Filter out <c>null</c> values from an enumerable stream.
    /// </summary>
    /// <typeparam name="T">The type of the enumerable.</typeparam>
    /// <param name="enumerable">The enumerable stream.</param>
    /// <returns>The enumerable stream void of <c>null</c> values.</returns>
    [return: NotNullIfNotNull(nameof(enumerable))]
    public static IEnumerable<T>? WhereNotNull<T>(this IEnumerable<T?>? enumerable)
        => enumerable?.Where(a => a is not null).Select(a => a!);

    /// <summary>
    /// Filter out <c>null</c> values from an enumerable stream.
    /// </summary>
    /// <typeparam name="T">The type of the enumerable.</typeparam>
    /// <param name="enumerable">The enumerable stream.</param>
    /// <returns>The enumerable stream void of <c>null</c> values.</returns>
    [return: NotNullIfNotNull(nameof(enumerable))]
    public static IEnumerable<T>? WhereNotNull<T>(this IEnumerable<T?>? enumerable) where T : struct
        => enumerable?.Where(a => a is not null).Select(a => a!.Value);

    /// <summary>
    /// Filter out <c>null</c> and default values from an enumerable stream.
    /// </summary>
    /// <typeparam name="T">The type of the enumerable.</typeparam>
    /// <param name="enumerable">The enumerable stream.</param>
    /// <returns>The enumerable stream void of <c>null</c> and default values.</returns>
    [return: NotNullIfNotNull(nameof(enumerable))]
    public static IEnumerable<T>? WhereNotNullOrDefault<T>(this IEnumerable<T?>? enumerable)
        => enumerable?.Where(a => a is not null && !Equals(a, default(T))).Select(a => a!);

    /// <summary>
    /// Filter out <c>null</c> and default values from an enumerable stream.
    /// </summary>
    /// <typeparam name="T">The type of the enumerable.</typeparam>
    /// <param name="enumerable">The enumerable stream.</param>
    /// <returns>The enumerable stream void of <c>null</c> and default values.</returns>
    [return: NotNullIfNotNull(nameof(enumerable))]
    public static IEnumerable<T>? WhereNotNullOrDefault<T>(this IEnumerable<T?>? enumerable) where T : struct
        => enumerable?.Where(a => a is not null && !Equals(a, default(T))).Select(a => a!.Value);
}
