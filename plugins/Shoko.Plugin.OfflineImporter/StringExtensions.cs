using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Shoko.Plugin.OfflineImporter;

/// <summary>
/// Extensions for <see cref="string"/>.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Matches the regular expression pattern against the input string.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="regexPattern">The regular expression pattern.</param>
    /// <returns>The match.</returns>
    public static Match Match(this string input, [StringSyntax("Regex")] string regexPattern)
        => Regex.Match(input, regexPattern);

    /// <summary>
    /// Replaces the first occurrence of a pattern defined by a regular expression with a replacement string.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="regex">The regular expression.</param>
    /// <param name="replacement">The replacement string.</param>
    /// <returns>The modified string.</returns>
    public static string Replace(this string input, Regex regex, string replacement)
        => regex.Replace(input, replacement);

    /// <summary>
    /// Concatenates the elements of a collection, using the specified separator between each element.
    /// </summary>
    /// <param name="list">The collection to concatenate.</param>
    /// <param name="separator">The separator to use.</param>
    /// <returns>The concatenated string.</returns>
    public static string Join(this IEnumerable<string> list, char separator)
        => string.Join(separator, list);
}
