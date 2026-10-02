using System;

namespace Shoko.Abstractions.Utilities;

/// <summary>
///   Reports into a slice of a parent progress: 0 to 100 here becomes
///   <see cref="Start"/> to <see cref="End"/> there. All values are
///   percentages from 0 to 100, as a scheduled action's progress takes them.
/// </summary>
/// <remarks>
///   Values outside 0 to 100 are clamped. It neither throttles nor holds the
///   value back from going down; it only maps.
/// </remarks>
public sealed class RangeProgress : IProgress<decimal>
{
    #region Fields

    private readonly IProgress<decimal>? _parent;

    #endregion

    #region Constructors

    /// <summary>
    ///   Creates a slice of <paramref name="parent"/>.
    /// </summary>
    /// <param name="parent">
    ///   Takes the mapped values, or <see langword="null"/> to drop them.
    /// </param>
    /// <param name="start">Where 0 lands on the parent, from 0 to 100.</param>
    /// <param name="end">
    ///   Where 100 lands on the parent, from <paramref name="start"/> to 100.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <paramref name="start"/> or <paramref name="end"/> is outside 0 to
    ///   100, or <paramref name="end"/> is below <paramref name="start"/>.
    /// </exception>
    public RangeProgress(IProgress<decimal>? parent, decimal start, decimal end)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(start, 0m);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, 100m);
        ArgumentOutOfRangeException.ThrowIfLessThan(end, start);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(end, 100m);
        _parent = parent;
        Start = start;
        End = end;
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Where 0 lands on the parent.
    /// </summary>
    public decimal Start { get; }

    /// <summary>
    ///   Where 100 lands on the parent.
    /// </summary>
    public decimal End { get; }

    #endregion

    #region Methods

    /// <summary>
    ///   Reports <paramref name="value"/>, mapped into the slice, to the
    ///   parent.
    /// </summary>
    /// <param name="value">The progress within the slice, from 0 to 100.</param>
    public void Report(decimal value)
        => _parent?.Report(Start + ((End - Start) * Math.Clamp(value, 0m, 100m) / 100m));

    #endregion
}
