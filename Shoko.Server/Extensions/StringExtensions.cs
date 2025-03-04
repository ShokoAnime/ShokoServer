using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Shoko.Server.Extensions;

public static class StringExtensions
{
    public static string Replace(this string input, Regex regex, string replacement, int count, int startAt)
        => regex.Replace(input, replacement, count, startAt);

    public static string Replace(this string input, Regex regex, MatchEvaluator evaluator, int count, int startAt)
        => regex.Replace(input, evaluator, count, startAt);

    public static string Replace(this string input, Regex regex, MatchEvaluator evaluator, int count)
        => regex.Replace(input, evaluator, count);

    public static string Replace(this string input, Regex regex, MatchEvaluator evaluator)
        => regex.Replace(input, evaluator);

    public static string Replace(this string input, Regex regex, string replacement)
        => regex.Replace(input, replacement);

    public static string Replace(this string input, Regex regex, string replacement, int count)
        => regex.Replace(input, replacement, count);

    public static void Deconstruct<T>(this IEnumerable<T> enumerable, out T first, out T second)
    {
        var list = enumerable is IReadOnlyList<T> readonlyList ? readonlyList : enumerable.ToList();
        first = list.Count > 0 ? list[0] : default;
        second = list.Count > 1 ? list[1] : default;
    }

    public static void Deconstruct<T>(this IEnumerable<T> enumerable, out T first, out T second, out T third)
    {
        var list = enumerable is IReadOnlyList<T> readonlyList ? readonlyList : enumerable.ToList();
        first = list.Count > 0 ? list[0] : default;
        second = list.Count > 1 ? list[1] : default;
        third = list.Count > 2 ? list[2] : default;
    }

    public static void Deconstruct<T>(this IEnumerable<T> enumerable, out T first, out T second, out T third, out T forth)
    {
        var list = enumerable is IReadOnlyList<T> readonlyList ? readonlyList : enumerable.ToList();
        first = list.Count > 0 ? list[0] : default;
        second = list.Count > 1 ? list[1] : default;
        third = list.Count > 2 ? list[2] : default;
        forth = list.Count > 3 ? list[3] : default;
    }

    public static void Deconstruct<T>(this IEnumerable<T> enumerable, out T first, out T second, out T third, out T forth, out T fifth)
    {
        var list = enumerable is IReadOnlyList<T> readonlyList ? readonlyList : enumerable.ToList();
        first = list.Count > 0 ? list[0] : default;
        second = list.Count > 1 ? list[1] : default;
        third = list.Count > 2 ? list[2] : default;
        forth = list.Count > 3 ? list[3] : default;
        fifth = list.Count > 4 ? list[4] : default;
    }

    public static string Join(this IEnumerable<string> list, char separator)
        => string.Join(separator, list);

    public static string Join(this IEnumerable<string> list, string separator)
        => string.Join(separator, list);

    public static string ToISO8601Date(this DateTime dt)
    {
        return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static bool FindInEnumerable(this IEnumerable<string> items, IEnumerable<string> list)
    {
        if (items == null || list == null)
        {
            return false;
        }

        HashSet<string> listHash;
        HashSet<string> itemHash;
        if (list is HashSet<string> listSet && Equals(listSet.Comparer, StringComparer.InvariantCultureIgnoreCase))
        {
            listHash = listSet;
        }
        else
        {
            listHash = list.Select(a => a.Trim())
                        .Where(a => !string.IsNullOrWhiteSpace(a))
                        .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        }

        if (items is HashSet<string> itemSet && Equals(itemSet.Comparer, StringComparer.InvariantCultureIgnoreCase))
        {
            itemHash = itemSet;
        }
        else
        {
            itemHash = items.Select(a => a.Trim())
                                   .Where(a => !string.IsNullOrWhiteSpace(a))
                                   .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        }

        return listHash.Overlaps(itemHash);
    }

    public static bool FindIn(this string item, IEnumerable<string> list)
    {
        return list.Contains(item, StringComparer.InvariantCultureIgnoreCase);
    }

    public static bool IsWithinErrorMargin(this DateTime value1, DateTime value2, TimeSpan error)
    {
        if (value1 > value2)
        {
            return value1 - value2 <= error;
        }

        return value2 - value1 <= error;
    }

    public static bool EqualsInvariantIgnoreCase(this string value1, string value2)
    {
        return value1.Equals(value2, StringComparison.InvariantCultureIgnoreCase);
    }

    public static string CamelCaseToNatural(this string text, bool preserveAcronyms = true)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        var newText = new StringBuilder(text.Length * 2);
        newText.Append(text[0]);
        for (var i = 1; i < text.Length; i++)
        {
            if (char.IsUpper(text[i]))
                if ((text[i - 1] != ' ' && !char.IsUpper(text[i - 1])) ||
                    (preserveAcronyms && char.IsUpper(text[i - 1]) &&
                     i < text.Length - 1 && !char.IsUpper(text[i + 1])))
                    newText.Append(' ');
            newText.Append(text[i]);
        }
        return newText.ToString();
    }

    public static string TrimEnd(this string inputText, string value, StringComparison comparisonType = StringComparison.CurrentCultureIgnoreCase)
    {
        if (string.IsNullOrEmpty(value)) return inputText;
        while (!string.IsNullOrEmpty(inputText) && inputText.EndsWith(value, comparisonType)) inputText = inputText[..^value.Length];

        return inputText;
    }
}
