using System;

namespace Shoko.Abstractions.Utilities;

/// <summary>
///   Shorthands for the progress helpers, on any
///   <see cref="IProgress{T}"/> of <see cref="decimal"/> taking a
///   percentage from 0 to 100, as a scheduled action's progress does.
/// </summary>
public static class ProgressExtensions
{
    #region Methods

    /// <summary>
    ///   Creates a <see cref="RangeProgress"/> reporting into a slice of
    ///   <paramref name="progress"/>.
    /// </summary>
    /// <param name="progress">The parent, or <see langword="null"/> to drop the values.</param>
    /// <param name="start">Where 0 lands on the parent, from 0 to 100.</param>
    /// <param name="end">Where 100 lands on the parent, from <paramref name="start"/> to 100.</param>
    /// <returns>The slice.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <paramref name="start"/> or <paramref name="end"/> is outside 0 to
    ///   100, or <paramref name="end"/> is below <paramref name="start"/>.
    /// </exception>
    public static RangeProgress Slice(this IProgress<decimal>? progress, decimal start, decimal end)
        => new(progress, start, end);

    /// <summary>
    ///   Creates a <see cref="StagedProgress"/> splitting
    ///   <paramref name="progress"/> into stages by weight.
    /// </summary>
    /// <param name="progress">The parent, or <see langword="null"/> to drop the values.</param>
    /// <param name="weights">The stages' weights, none below zero and at least one above it.</param>
    /// <returns>The stages.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="weights"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="weights"/> is empty, has a negative weight, or adds
    ///   up to zero.
    /// </exception>
    public static StagedProgress InStages(this IProgress<decimal>? progress, params decimal[] weights)
        => new(progress, weights);

    /// <summary>
    ///   Creates an <see cref="ItemProgress"/> counting
    ///   <paramref name="total"/> items into <paramref name="progress"/>.
    /// </summary>
    /// <param name="progress">The parent, or <see langword="null"/> to drop the values.</param>
    /// <param name="total">How many items there are, zero or more.</param>
    /// <returns>The counter, which has reported nothing yet.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="total"/> is negative.</exception>
    public static ItemProgress ForItems(this IProgress<decimal>? progress, int total)
        => new(progress, total);

    /// <summary>
    ///   Creates a <see cref="ThrottledProgress"/> in front of
    ///   <paramref name="progress"/>.
    /// </summary>
    /// <param name="progress">The parent, or <see langword="null"/> to drop the values.</param>
    /// <param name="interval">
    ///   The least time between two values passed on, or
    ///   <see langword="null"/> for <see cref="ThrottledProgress.DefaultInterval"/>.
    /// </param>
    /// <returns>The throttle.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> is negative.</exception>
    public static ThrottledProgress Throttle(this IProgress<decimal>? progress, TimeSpan? interval = null)
        => new(progress, interval);

    #endregion
}
