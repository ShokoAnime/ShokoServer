using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;

namespace Shoko.Server.Extensions
{
    public static class StringExtensions
    {
        public static bool Contains(this string item, string other, StringComparison comparer)
        {
            if (item == null || other == null) return false;
            return item.IndexOf(other, comparer) >= 0;
        }

        public static void ShallowCopyTo(this object s, object d)
        {
            foreach (PropertyInfo pis in s.GetType().GetProperties())
            {
                foreach (PropertyInfo pid in d.GetType().GetProperties())
                {
                    if (pid.Name == pis.Name)
                        pid.GetSetMethod().Invoke(d, new[] {pis.GetGetMethod().Invoke(s, null)});
                }
            }
        }

        public static void AddRange<K, V>(this IDictionary<K, V> dict, IDictionary<K, V> otherdict)
        {
            if (dict == null || otherdict == null) return;
            otherdict.ForEach(a =>
            {
                if (!dict.ContainsKey(a.Key)) dict.Add(a.Key, a.Value);
            });
        }

        public static bool FindInEnumerable(this IEnumerable<string> items, IEnumerable<string> list)
        {
            if (items == null || list == null) return false;
            // Trim, to lower in both lists, remove null and empty strings
            HashSet<string> listhash = list.Select(a => a.ToLowerInvariant().Trim())
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
            HashSet<string> itemhash = items.Select(a => a.ToLowerInvariant().Trim())
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
            return listhash.Overlaps(itemhash);
        }

        public static bool FindInEnumerable(this IEnumerable<int> items, IEnumerable<int> list)
        {
            if (items == null || list == null) return false;
            return list.ToHashSet().Overlaps(items.ToHashSet());
        }

        public static bool FindIn(this string item, IEnumerable<string> list)
        {
            return list.Contains(item, StringComparer.InvariantCultureIgnoreCase);
        }

        public static int? ParseNullableInt(this string input)
        {
            return int.TryParse(input, out int output) ? output : (int?)null;
        }

        public static bool IsWithinErrorMargin(this DateTime value1, DateTime value2, TimeSpan error)
        {
            if (value1 > value2) return value1 - value2 <= error;
            return value2 - value1 <= error;
        }

        public static bool EqualsInvariantIgnoreCase(this string value1, string value2) =>
            value1.Equals(value2, StringComparison.InvariantCultureIgnoreCase);
        
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
    }
}
