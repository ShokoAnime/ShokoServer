
namespace Shoko.Abstractions.Extensions;

/// <summary>
/// Extension methods for numbers.
/// </summary>
public static class NumberExtensions
{
    /// <summary>
    /// Pads the number with zeroes and returns it as a string.
    /// </summary>
    /// <param name="num">Number to pad.</param>
    /// <param name="total">The highest number that num can be, used to determine how many zeroes to add.</param>
    /// <returns>The padded number as a string.</returns>
    public static string PadZeroes(this int num, int total)
    {
        var zeroPadding = total.ToString().Length;
        return num.ToString().PadLeft(zeroPadding, '0');
    }
}
