using System;

namespace Shoko.Server.Utilities;

/// <summary>
///   Tolerance-based comparisons for floating point values.
/// </summary>
internal static class FloatingPoint
{
    public const double Epsilon = 1e-6;

    public static bool AreEqual(double a, double b, double epsilon = Epsilon)
        => a.CompareTo(b) == 0 || Math.Abs(a - b) < epsilon;

    public static bool AreEqual(double? a, double? b, double epsilon = Epsilon)
        => a.HasValue && b.HasValue ? AreEqual(a.Value, b.Value, epsilon) : a.HasValue == b.HasValue;
}
