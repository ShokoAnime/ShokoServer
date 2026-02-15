using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shoko.Abstractions.Utilities;

namespace Shoko.Abstractions.Extensions;

/// <summary>
/// Extension methods for strings.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// The mapping used for replacing invalid path characters with unicode alternatives
    /// </summary>
    public static Dictionary<string, string> InvalidPathCharacterMap = new()
    {
        { "*", "\u2605" }, // ★ (BLACK STAR)
        { "|", "\u00a6" }, // ¦ (BROKEN BAR)
        { "\\", "\u29F9" }, // ⧹ (BIG REVERSE SOLIDUS)
        { "/", "\u2044" }, // ⁄ (FRACTION SLASH)
        { ":", "\u0589" }, // ։ (ARMENIAN FULL STOP)
        { "\"", "\u2033" }, // ″ (DOUBLE PRIME)
        { ">", "\u203a" }, // › (SINGLE RIGHT-POINTING ANGLE QUOTATION MARK)
        { "<", "\u2039" }, // ‹ (SINGLE LEFT-POINTING ANGLE QUOTATION MARK)
        { "?", "\uff1f" }, // ？ (FULL WIDTH QUESTION MARK)
    };

    /// <summary>
    /// Remove invalid path characters.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The sanitized path.</returns>
    public static string RemoveInvalidPathCharacters(this string path)
    {
        var ret = path;
        foreach (var key in InvalidPathCharacterMap.Keys)
            ret = ret.Replace(key, string.Empty);
        while (ret.EndsWith(".", StringComparison.Ordinal))
            ret = ret.Substring(0, ret.Length - 1);
        return ret.Trim();
    }

    /// <summary>
    /// Replace invalid path characters.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The sanitized path.</returns>
    public static string ReplaceInvalidPathCharacters(this string path)
    {
        var ret = path;
        foreach (var kvp in InvalidPathCharacterMap)
            ret = ret.Replace(kvp.Key, kvp.Value);
        ret = ret.Replace("...", "\u2026"); // … (HORIZONTAL ELLIPSIS))
        if (ret.StartsWith(".", StringComparison.Ordinal)) // U+002E
            ret = "․" + ret.Substring(1, ret.Length - 1); // U+2024
        if (ret.EndsWith(".", StringComparison.Ordinal)) // U+002E
            ret = ret.Substring(0, ret.Length - 1) + "․"; // U+2024
        return ret.Trim();
    }

    /// <summary>
    /// Replaces all occurrences of a pattern defined by a regular expression
    /// with a replacement string.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="regex">The regular expression.</param>
    /// <param name="replacement">The replacement string.</param>
    /// <returns>The modified string.</returns>
    public static string Replace(this string input, Regex regex, string replacement)
        => regex.Replace(input, replacement);

    /// <summary>
    /// Replaces a specified maximum number of occurrences of a pattern defined by a regular expression
    /// with a replacement string.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="regex">The regular expression.</param>
    /// <param name="replacement">The replacement string.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <returns>The modified string.</returns>
    public static string Replace(this string input, Regex regex, string replacement, int count)
        => regex.Replace(input, replacement, count);

    /// <summary>
    /// Replaces all occurrences of a pattern defined by a regular expression
    /// with the result of a match evaluator delegate.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="regex">The regular expression.</param>
    /// <param name="evaluator">The match evaluator delegate.</param>
    /// <returns>The modified string.</returns>
    public static string Replace(this string input, Regex regex, MatchEvaluator evaluator)
        => regex.Replace(input, evaluator);

    /// <summary>
    /// Replaces a specified maximum number of occurrences of a pattern defined by a regular expression
    /// with the result of a match evaluator delegate.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="regex">The regular expression.</param>
    /// <param name="evaluator">The match evaluator delegate.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <returns>The modified string.</returns>
    public static string Replace(this string input, Regex regex, MatchEvaluator evaluator, int count)
        => regex.Replace(input, evaluator, count);

    /// <summary>
    /// Replaces a specified maximum number of occurrences of a pattern defined by a regular expression
    /// with a replacement string, starting from a specified position.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="regex">The regular expression.</param>
    /// <param name="replacement">The replacement string.</param>
    /// <param name="count">The maximum number of replacements to perform.</param>
    /// <param name="startAt">The position to start the replacement from.</param>
    /// <returns>The modified string.</returns>
    public static string Replace(this string input, Regex regex, string replacement, int count, int startAt)
        => regex.Replace(input, replacement, count, startAt);

    /// <summary>
    /// Deconstructs the first element of the list.
    /// </summary>
    /// <param name="list">The list to deconstruct.</param>
    /// <param name="first">The first element of the list.</param>
    public static void Deconstruct(this IList<string> list, out string first)
    {
        first = list.Count > 0 ? list[0] : string.Empty;
    }

    /// <summary>
    /// Deconstructs the first two elements of the list.
    /// </summary>
    /// <param name="list">The list to deconstruct.</param>
    /// <param name="first">The first element of the list.</param>
    /// <param name="second">The second element of the list.</param>
    public static void Deconstruct(this IList<string> list, out string first, out string second)
    {
        first = list.Count > 0 ? list[0] : string.Empty;
        second = list.Count > 1 ? list[1] : string.Empty;
    }

    /// <summary>
    /// Deconstructs the first three elements of the list.
    /// </summary>
    /// <param name="list">The list to deconstruct.</param>
    /// <param name="first">The first element of the list.</param>
    /// <param name="second">The second element of the list.</param>
    /// <param name="third">The third element of the list.</param>
    public static void Deconstruct(this IList<string> list, out string first, out string second, out string third)
    {
        first = list.Count > 0 ? list[0] : string.Empty;
        second = list.Count > 1 ? list[1] : string.Empty;
        third = list.Count > 2 ? list[2] : string.Empty;
    }

    /// <summary>
    /// Deconstructs the first four elements of the list.
    /// </summary>
    /// <param name="list">The list to deconstruct.</param>
    /// <param name="first">The first element of the list.</param>
    /// <param name="second">The second element of the list.</param>
    /// <param name="third">The third element of the list.</param>
    /// <param name="forth">The fourth element of the list.</param>
    public static void Deconstruct(this IList<string> list, out string first, out string second, out string third, out string forth)
    {
        first = list.Count > 0 ? list[0] : string.Empty;
        second = list.Count > 1 ? list[1] : string.Empty;
        third = list.Count > 2 ? list[2] : string.Empty;
        forth = list.Count > 3 ? list[3] : string.Empty;
    }

    /// <summary>
    /// Deconstructs the first five elements of the list.
    /// </summary>
    /// <param name="list">The list to deconstruct.</param>
    /// <param name="first">The first element of the list.</param>
    /// <param name="second">The second element of the list.</param>
    /// <param name="third">The third element of the list.</param>
    /// <param name="forth">The fourth element of the list.</param>
    /// <param name="fifth">The fifth element of the list.</param>
    public static void Deconstruct(this IList<string> list, out string first, out string second, out string third, out string forth, out string fifth)
    {
        first = list.Count > 0 ? list[0] : string.Empty;
        second = list.Count > 1 ? list[1] : string.Empty;
        third = list.Count > 2 ? list[2] : string.Empty;
        forth = list.Count > 3 ? list[3] : string.Empty;
        fifth = list.Count > 4 ? list[4] : string.Empty;
    }

    /// <summary>
    /// Deconstructs the first element of the enumerable.
    /// </summary>
    /// <param name="enumerable">The enumerable to deconstruct.</param>
    /// <param name="first">The first element of the enumerable.</param>
    public static void Deconstruct<T>(this IEnumerable<T> enumerable, out T first)
    {
        var list = enumerable is IReadOnlyList<T> readonlyList ? readonlyList : enumerable.ToList();
        first = list.Count > 0 ? list[0] : default!;
    }

    /// <summary>
    /// Deconstructs the first two elements of the enumerable.
    /// </summary>
    /// <param name="enumerable">The enumerable to deconstruct.</param>
    /// <param name="first">The first element of the enumerable.</param>
    /// <param name="second">The second element of the enumerable.</param>
    public static void Deconstruct<T>(this IEnumerable<T> enumerable, out T first, out T second)
    {
        var list = enumerable is IReadOnlyList<T> readonlyList ? readonlyList : enumerable.ToList();
        first = list.Count > 0 ? list[0] : default!;
        second = list.Count > 1 ? list[1] : default!;
    }

    /// <summary>
    /// Deconstructs the first three elements of the enumerable.
    /// </summary>
    /// <param name="enumerable">The enumerable to deconstruct.</param>
    /// <param name="first">The first element of the enumerable.</param>
    /// <param name="second">The second element of the enumerable.</param>
    /// <param name="third">The third element of the enumerable.</param>
    public static void Deconstruct<T>(this IEnumerable<T> enumerable, out T first, out T second, out T third)
    {
        var list = enumerable is IReadOnlyList<T> readonlyList ? readonlyList : enumerable.ToList();
        first = list.Count > 0 ? list[0] : default!;
        second = list.Count > 1 ? list[1] : default!;
        third = list.Count > 2 ? list[2] : default!;
    }

    /// <summary>
    /// Deconstructs the first four elements of the enumerable.
    /// </summary>
    /// <param name="enumerable">The enumerable to deconstruct.</param>
    /// <param name="first">The first element of the enumerable.</param>
    /// <param name="second">The second element of the enumerable.</param>
    /// <param name="third">The third element of the enumerable.</param>
    /// <param name="forth">The fourth element of the enumerable.</param>
    public static void Deconstruct<T>(this IEnumerable<T> enumerable, out T first, out T second, out T third, out T forth)
    {
        var list = enumerable is IReadOnlyList<T> readonlyList ? readonlyList : enumerable.ToList();
        first = list.Count > 0 ? list[0] : default!;
        second = list.Count > 1 ? list[1] : default!;
        third = list.Count > 2 ? list[2] : default!;
        forth = list.Count > 3 ? list[3] : default!;
    }

    /// <summary>
    /// Deconstructs the first five elements of the enumerable.
    /// </summary>
    /// <param name="enumerable">The enumerable to deconstruct.</param>
    /// <param name="first">The first element of the enumerable.</param>
    /// <param name="second">The second element of the enumerable.</param>
    /// <param name="third">The third element of the enumerable.</param>
    /// <param name="forth">The fourth element of the enumerable.</param>
    /// <param name="fifth">The fifth element of the enumerable.</param>
    public static void Deconstruct<T>(this IEnumerable<T> enumerable, out T first, out T second, out T third, out T forth, out T fifth)
    {
        var list = enumerable is IReadOnlyList<T> readonlyList ? readonlyList : enumerable.ToList();
        first = list.Count > 0 ? list[0] : default!;
        second = list.Count > 1 ? list[1] : default!;
        third = list.Count > 2 ? list[2] : default!;
        forth = list.Count > 3 ? list[3] : default!;
        fifth = list.Count > 4 ? list[4] : default!;
    }

    /// <summary>
    ///   Joins a sequence of strings together using a separator.
    /// </summary>
    /// <param name="list">The sequence of strings to join.</param>
    /// <param name="separator">The separator to use.</param>
    /// <returns>The joined string.</returns>
    public static string Join(this IEnumerable<string> list, char separator)
        => string.Join(separator, list);

    /// <summary>
    ///   Joins a sequence of strings together using a separator.
    /// </summary>
    /// <param name="list">The sequence of strings to join.</param>
    /// <param name="separator">The separator to use, or <c>null</c> to use the default separator.</param>
    /// <returns>The joined string.</returns>
    public static string Join(this IEnumerable<string> list, string? separator)
        => string.Join(separator, list);

    /// <summary>
    ///   Joins a sequence of strings together using a separator, starting at a given index.
    /// </summary>
    /// <param name="list">The sequence of strings to join.</param>
    /// <param name="separator">The separator to use.</param>
    /// <param name="startIndex">The index at which to start joining.</param>
    /// <param name="count">The number of strings to join.</param>
    /// <returns>The joined string.</returns>
    public static string Join(this IEnumerable<string> list, char separator, int startIndex, int count)
        => string.Join(separator, list, startIndex, count);

    /// <summary>
    ///   Joins a sequence of strings together using a separator, starting at a given index.
    /// </summary>
    /// <param name="list">The sequence of strings to join.</param>
    /// <param name="separator">The separator to use, or <c>null</c> to use the default separator.</param>
    /// <param name="startIndex">The index at which to start joining.</param>
    /// <param name="count">The number of strings to join.</param>
    /// <returns>The joined string.</returns>
    public static string Join(this IEnumerable<string> list, string? separator, int startIndex, int count)
        => string.Join(separator, list, startIndex, count);

    /// <summary>
    ///   Joins a sequence of characters together using a separator.
    /// </summary>
    /// <param name="list">The sequence of characters to join.</param>
    /// <param name="separator">The separator to use.</param>
    /// <returns>The joined string.</returns>
    public static string Join(this IEnumerable<char> list, char separator)
        => string.Join(separator.ToString(), list);

    /// <summary>
    ///   Joins a sequence of characters together using a separator.
    /// </summary>
    /// <param name="list">The sequence of characters to join.</param>
    /// <param name="separator">The separator to use, or <c>null</c> to use the default separator.</param>
    /// <returns>The joined string.</returns>
    public static string Join(this IEnumerable<char> list, string? separator)
        => string.Join(separator, list);

    /// <summary>
    ///   Joins a sequence of characters together using a separator, starting at a given index.
    /// </summary>
    /// <param name="list">The sequence of characters to join.</param>
    /// <param name="separator">The separator to use.</param>
    /// <param name="startIndex">The index at which to start joining.</param>
    /// <param name="count">The number of characters to join.</param>
    /// <returns>The joined string.</returns>
    public static string Join(this IEnumerable<char> list, char separator, int startIndex, int count)
        => string.Join(separator.ToString(), list, startIndex, count);

    /// <summary>
    ///   Joins a sequence of characters together using a separator, starting at a given index.
    /// </summary>
    /// <param name="list">The sequence of characters to join.</param>
    /// <param name="separator">The separator to use, or <c>null</c> to use the default separator.</param>
    /// <param name="startIndex">The index at which to start joining.</param>
    /// <param name="count">The number of characters to join.</param>
    /// <returns>The joined string.</returns>
    public static string Join(this IEnumerable<char> list, string? separator, int startIndex, int count)
        => string.Join(separator, list, startIndex, count);

    /// <summary>
    ///   Splits a string using a regular expression.
    /// </summary>
    /// <param name="input">
    ///   The string to split.
    /// </param>
    /// <param name="regex">
    ///   The regular expression to split on.
    /// </param>
    /// <param name="options">
    ///   The options.
    /// </param>
    /// <returns>
    ///   The split string.
    /// </returns>
    public static string[] Split(this string input, Regex regex, StringSplitOptions options = StringSplitOptions.None)
    {
        IEnumerable<string> list = regex.Split(input);
        if (options.HasFlag(StringSplitOptions.TrimEntries))
            list = list.Select(x => x.Trim());
        if (options.HasFlag(StringSplitOptions.RemoveEmptyEntries))
            list = list.Where(x => !string.IsNullOrEmpty(x));
        return list is string[] result ? result : list.ToArray();
    }

    /// <summary>
    ///   Removes the specified value from the start of the string.
    /// </summary>
    /// <param name="inputText">
    ///   The string to trim.
    /// </param>
    /// <param name="value">
    ///   The value to remove.
    /// </param>
    /// <param name="comparisonType">
    ///   The comparison type.
    /// </param>
    /// <returns>
    ///   The trimmed string.
    /// </returns>
    public static string TrimStart(this string inputText, string value, StringComparison comparisonType = StringComparison.CurrentCultureIgnoreCase)
    {
        if (string.IsNullOrEmpty(value)) return inputText;
        while (!string.IsNullOrEmpty(inputText) && inputText.StartsWith(value, comparisonType)) inputText = inputText[value.Length..];

        return inputText;
    }

    /// <summary>
    ///   Removes the specified value from the end of the string.
    /// </summary>
    /// <param name="inputText">
    ///   The string to trim.
    /// </param>
    /// <param name="value">
    ///   The value to remove.
    /// </param>
    /// <param name="comparisonType">
    ///   The comparison type.
    /// </param>
    /// <returns>
    ///   The trimmed string.
    /// </returns>
    public static string TrimEnd(this string inputText, string value, StringComparison comparisonType = StringComparison.CurrentCultureIgnoreCase)
    {
        if (string.IsNullOrEmpty(value)) return inputText;
        while (!string.IsNullOrEmpty(inputText) && inputText.EndsWith(value, comparisonType)) inputText = inputText[..^value.Length];

        return inputText;
    }

    /// <summary>
    /// Generates a version 3 UUID from <paramref name="input"/> in the specified namespace.
    /// </summary>
    /// <remarks>
    /// RFC definition of a version 3 UUID:
    /// <br/>
    /// https://www.rfc-editor.org/rfc/rfc9562.html#section-5.3
    /// </remarks>
    /// <param name="input">Input text.</param>
    /// <param name="namespaceGuid">UUID namespace to use.</param>
    /// <returns>The new UUID.</returns>
    public static Guid ToUuidV3(this string input, Guid namespaceGuid = default)
        => UuidUtility.GetV3(input, namespaceGuid);

    /// <summary>
    /// Generates a version 5 UUID from <paramref name="input"/> in the specified namespace.
    /// </summary>
    /// <remarks>
    /// RFC definition of a version 5 UUID:
    /// <br/>
    /// https://www.rfc-editor.org/rfc/rfc9562.html#section-5.5
    /// </remarks>
    /// <param name="input">Input text.</param>
    /// <param name="namespaceGuid">UUID namespace to use.</param>
    /// <returns>The new UUID.</returns>
    public static Guid ToUuidV5(this string input, Guid namespaceGuid = default)
        => UuidUtility.GetV5(input, namespaceGuid);
}
