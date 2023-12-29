using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Shoko.Commons.Extensions;

namespace Shoko.Server.Extensions;

public static class StringExtensions
{
    public static void Deconstruct(this IList<string> list, out string first, out string second, out string third, out string forth, out IList<string> rest) {
        first = list.Count > 0 ? list[0] : "";
        second = list.Count > 1 ? list[1] : "";
        third = list.Count > 2 ? list[2] : "";
        forth = list.Count > 3 ? list[3] : "";
        rest = list.Skip(4).ToList();
    }
    
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
    
    public static string CamelCaseToNatural(this string text, bool preserveAcronyms=true)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        StringBuilder newText = new StringBuilder(text.Length * 2);
        newText.Append(text[0]);
        for (int i = 1; i < text.Length; i++)
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
    
    public static string TrimStart(this string inputText, string value, StringComparison comparisonType = StringComparison.CurrentCultureIgnoreCase)
    {
        if (string.IsNullOrEmpty(value)) return inputText;
        while (!string.IsNullOrEmpty(inputText) && inputText.StartsWith(value, comparisonType)) inputText = inputText[(value.Length - 1)..];

        return inputText;
    }

    public static string TrimEnd(this string inputText, string value, StringComparison comparisonType = StringComparison.CurrentCultureIgnoreCase)
    {
        if (string.IsNullOrEmpty(value)) return inputText;
        while (!string.IsNullOrEmpty(inputText) && inputText.EndsWith(value, comparisonType)) inputText = inputText[..^value.Length];

        return inputText;
    }

    public static string Trim(this string inputText, string value, StringComparison comparisonType = StringComparison.CurrentCultureIgnoreCase)
    {
        return TrimStart(TrimEnd(inputText, value, comparisonType), value, comparisonType);
    }
}
