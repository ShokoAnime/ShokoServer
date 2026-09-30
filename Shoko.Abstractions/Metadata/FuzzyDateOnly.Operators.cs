namespace Shoko.Abstractions.Metadata;

public readonly partial struct FuzzyDateOnly
{
    #region Operators

    /// <summary>
    ///   Checks whether two dates are both missing or have the same parts.
    /// </summary>
    /// <param name="left">The first date.</param>
    /// <param name="right">The second date.</param>
    /// <returns>Whether they are equal.</returns>
    public static bool operator ==(FuzzyDateOnly? left, FuzzyDateOnly? right)
        => left.HasValue == right.HasValue && (!left.HasValue || left.Value.Equals(right!.Value));

    /// <summary>
    ///   Checks whether two dates differ, or only one of them is missing.
    /// </summary>
    /// <param name="left">The first date.</param>
    /// <param name="right">The second date.</param>
    /// <returns>Whether they differ.</returns>
    public static bool operator !=(FuzzyDateOnly? left, FuzzyDateOnly? right)
        => !(left == right);

    /// <summary>
    ///   Checks whether one date sorts before another, as
    ///   <see cref="CompareTo(FuzzyDateOnly)"/> orders them. A missing date
    ///   sorts first.
    /// </summary>
    /// <param name="left">The first date.</param>
    /// <param name="right">The second date.</param>
    /// <returns>Whether <paramref name="left"/> sorts first.</returns>
    public static bool operator <(FuzzyDateOnly? left, FuzzyDateOnly? right)
        => Compare(left, right) < 0;

    /// <summary>
    ///   Checks whether one date sorts after another, as
    ///   <see cref="CompareTo(FuzzyDateOnly)"/> orders them. A missing date
    ///   sorts first.
    /// </summary>
    /// <param name="left">The first date.</param>
    /// <param name="right">The second date.</param>
    /// <returns>Whether <paramref name="left"/> sorts last.</returns>
    public static bool operator >(FuzzyDateOnly? left, FuzzyDateOnly? right)
        => Compare(left, right) > 0;

    /// <summary>
    ///   Checks whether one date sorts before another or equals it. A
    ///   missing date sorts first.
    /// </summary>
    /// <param name="left">The first date.</param>
    /// <param name="right">The second date.</param>
    /// <returns>Whether <paramref name="left"/> does not sort last.</returns>
    public static bool operator <=(FuzzyDateOnly? left, FuzzyDateOnly? right)
        => Compare(left, right) <= 0;

    /// <summary>
    ///   Checks whether one date sorts after another or equals it. A missing
    ///   date sorts first.
    /// </summary>
    /// <param name="left">The first date.</param>
    /// <param name="right">The second date.</param>
    /// <returns>Whether <paramref name="left"/> does not sort first.</returns>
    public static bool operator >=(FuzzyDateOnly? left, FuzzyDateOnly? right)
        => Compare(left, right) >= 0;

    private static int Compare(FuzzyDateOnly? left, FuzzyDateOnly? right)
    {
        if (left.HasValue != right.HasValue)
            return left.HasValue ? 1 : -1;

        return left.HasValue ? left.Value.CompareTo(right!.Value) : 0;
    }

    #endregion
}
