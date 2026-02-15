using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Shoko.Server.Extensions;

public static class StringExtensions
{
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
}
