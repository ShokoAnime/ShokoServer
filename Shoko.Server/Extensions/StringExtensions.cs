using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shoko.Commons.Extensions;

namespace Shoko.Server.Extensions;

public static class StringExtensions
{
    public static void Deconstruct(this IList<string> list, out string first, out IList<string> rest) {
        first = list.Count > 0 ? list[0] : "";
        rest = list.Skip(1).ToList();
    }

    public static void Deconstruct(this IList<string> list, out string first, out string second, out IList<string> rest) {
        first = list.Count > 0 ? list[0] : "";
        second = list.Count > 1 ? list[1] : "";
        rest = list.Skip(2).ToList();
    }

    public static void Deconstruct(this IList<string> list, out string first, out string second, out string third, out IList<string> rest) {
        first = list.Count > 0 ? list[0] : "";
        second = list.Count > 1 ? list[1] : "";
        third = list.Count > 2 ? list[2] : "";
        rest = list.Skip(3).ToList();
    }

    public static void Deconstruct(this IList<string> list, out string first, out string second, out string third, out string forth, out IList<string> rest) {
        first = list.Count > 0 ? list[0] : "";
        second = list.Count > 1 ? list[1] : "";
        third = list.Count > 2 ? list[2] : "";
        forth = list.Count > 3 ? list[3] : "";
        rest = list.Skip(4).ToList();
    }

    public static bool Contains(this string item, string other, StringComparison comparer)
    {
        if (item == null || other == null)
        {
            return false;
        }

        return item.IndexOf(other, comparer) >= 0;
    }

    public static void ShallowCopyTo(this object s, object d)
    {
        foreach (var pis in s.GetType().GetProperties())
        {
            foreach (var pid in d.GetType().GetProperties())
            {
                if (pid.Name == pis.Name)
                {
                    pid.GetSetMethod().Invoke(d, new[] { pis.GetGetMethod().Invoke(s, null) });
                }
            }
        }
    }

    public static void AddRange<K, V>(this IDictionary<K, V> dict, IDictionary<K, V> otherdict)
    {
        if (dict == null || otherdict == null)
        {
            return;
        }

        otherdict.ForEach(a =>
        {
            if (!dict.ContainsKey(a.Key))
            {
                dict.Add(a.Key, a.Value);
            }
        });
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

    public static bool FindInEnumerable(this IEnumerable<int> items, IEnumerable<int> list)
    {
        if (items == null || list == null)
        {
            return false;
        }

        return list.ToHashSet().Overlaps(items.ToHashSet());
    }

    public static bool FindIn(this string item, IEnumerable<string> list)
    {
        return list.Contains(item, StringComparer.InvariantCultureIgnoreCase);
    }

    public static int? ParseNullableInt(this string input)
    {
        return int.TryParse(input, out var output) ? output : (int?)null;
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

    public static string SplitCamelCaseToWords(this string strInput)
    {
        var strOutput = new StringBuilder();
        int intCurrentCharPos;
        var intLastCharPos = strInput.Length - 1;
        for (intCurrentCharPos = 0; intCurrentCharPos <= intLastCharPos; intCurrentCharPos++)
        {
            var chrCurrentInputChar = strInput[intCurrentCharPos];
            var chrPreviousInputChar = chrCurrentInputChar;

            if (intCurrentCharPos > 0)
            {
                chrPreviousInputChar = strInput[intCurrentCharPos - 1];
            }

            if (char.IsUpper(chrCurrentInputChar) && char.IsLower(chrPreviousInputChar))
            {
                strOutput.Append(' ');
            }

            strOutput.Append(chrCurrentInputChar);
        }

        return strOutput.ToString();
    }

    public static string GetSortName(this string name)
    {
        if (name.StartsWith("A ")) name = name[2..];
        if (name.StartsWith("An ")) name = name[3..];
        if (name.StartsWith("The ")) name = name[4..];
        return name;
    }
}
