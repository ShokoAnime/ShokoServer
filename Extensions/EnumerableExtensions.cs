using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shoko.Commons.Collections;

namespace Shoko.Commons.Extensions
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>
            (this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }

        public static IOrderedEnumerable<TSource> OrderByNatural<TSource>(this IEnumerable<TSource> items, Func<TSource, string> selector, StringComparer stringComparer = null)
        {
            var regex = new Regex(@"\d+", RegexOptions.Compiled);

            var maxDigits = (items.Select(selector)
                    .Where(sel => sel != null)
                    .SelectMany(sel => regex.Matches(sel).Cast<Match>(), (sel, match) => match?.Value?.Length ?? 0))
                .Concat(new[] {0}).Max();

            return items.OrderBy(i => selector(i) != null).ThenBy(i =>
                {
                    string sel = selector(i);
                    if (sel == null) return null;
                    return regex.Replace(sel, match => match.Value.PadLeft(maxDigits, '0'));
                }, stringComparer ?? StringComparer.CurrentCulture);
        }

        public static string ToRanges(this List<int> ints) {
            if (ints.Count < 1) return "";
            if (ints.Count == 1) return ints[0].ToString();
            ints.Sort();
            var lng = ints.Count;
            var fromnums = new List<int>();
            var tonums = new List<int>();
            for (var i = 0; i < lng - 1; i++) {
                if (i == 0)
                    fromnums.Add(ints[0]);
                if (ints[i + 1] > ints[i] + 1) {
                    tonums.Add(ints[i]);
                    fromnums.Add(ints[i + 1]);
                }
            }
            tonums.Add(ints[lng - 1]);
            return string.Join(", ", Enumerable.Range(0, tonums.Count).Select(
                i => fromnums[i].ToString() +
                     (tonums[i] == fromnums[i] ? "" : "-" + tonums[i].ToString())
            ));
        }

        public static ILookup<TKey, TSource> ToLazyLookup<TKey, TSource>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey> comparer = null)
        {
            return LazyLookup<TKey, TSource>.Create(source, keySelector, comparer);
        }

        public static ILookup<TKey, TElement> ToLazyLookup<TSource, TKey, TElement>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector,
            Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer = null)
        {
            return LazyLookup<TKey, TElement>.Create(source, keySelector, elementSelector, comparer);
        }

        /// <summary>
        /// Splits up the sequence into batches of the specified <paramref name="size"/>.
        /// </summary>
        /// <typeparam name="TSource">The type of items in <paramref name="source"/>.</typeparam>
        /// <param name="source">The sequence to whose items are to be split up into batches.</param>
        /// <param name="size">The maximum size for each batch.</param>
        /// <returns>A sequence of batched items.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is less than 1.</exception>
        public static IEnumerable<TSource[]> Batch<TSource>(this IEnumerable<TSource> source, int size)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (size < 1)
                throw new ArgumentOutOfRangeException(nameof(size), size, "The batch size must be >= 1");

            TSource[] bucket = null;
            var count = 0;

            foreach (var item in source)
            {
                if (bucket == null)
                {
                    bucket = new TSource[size];
                }

                bucket[count++] = item;

                if (count != size)
                {
                    continue;
                }

                yield return bucket;

                bucket = null;
                count = 0;
            }

            // Return the last bucket with remaining elements
            if (bucket != null && count > 0)
            {
                Array.Resize(ref bucket, count);

                yield return bucket;
            }
        }

        /// <summary>
        /// Calls <paramref name="action"/> for each item in <paramref name="source"/>.
        /// </summary>
        /// <typeparam name="TSource">The type of items in <paramref name="source"/>.</typeparam>
        /// <param name="source">The sequence of items to iterate.</param>
        /// <param name="action">The <see cref="Action{T}"/> to call for each item.</param>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="action"/> is <c>null</c>.</exception>
        public static void ForEach<TSource>(this IEnumerable<TSource> source, Action<TSource> action)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            foreach (TSource item in source)
            {
                action(item);
            }
        }

        /// <summary>
        /// Converts the specified sequence into a <see cref="HashSet{T}"/>
        /// </summary>
        /// <typeparam name="TSource">The type of items in <paramref name="source"/>.</typeparam>
        /// <param name="source">The sequence to convert to a <see cref="HashSet{T}"/></param>
        /// <param name="comparer">The optional <see cref="IEqualityComparer{T}"/> to use for comparing values.</param>
        /// <returns>The created <see cref="HashSet{T}"/> containing the distinct values from <paramref name="source"/>.</returns>
        public static HashSet<TSource> ToHashSet<TSource>(this IEnumerable<TSource> source, IEqualityComparer<TSource> comparer = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return new HashSet<TSource>(source, comparer);
        }

        /// <summary>
        /// Casts/converts the specified <see cref="IEnumerable{T}"/> to a <see cref="IReadOnlyCollection{T}"/>.
        /// </summary>
        /// <typeparam name="TSource">The type of items in <paramref name="source"/>.</typeparam>
        /// <param name="source">The sequence to cast/convert to a read only collection.</param>
        /// <returns>A <see cref="IReadOnlyCollection{T}"/> version of the specified sequence.</returns>
        public static IReadOnlyCollection<TSource> AsReadOnlyCollection<TSource>(this IEnumerable<TSource> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var readonlyColl = source as IReadOnlyCollection<TSource>;

            if (readonlyColl != null)
            {
                return readonlyColl;
            }

            return source.ToList();
        }

        public static IEnumerable<IEnumerable<T>> GetPermutations<T>(this IEnumerable<T> list, int length = -1)
        {
            if (length == -1) length = list.Count();
            if (length == 1) return list.Select(t => new[] { t });

            return GetPermutations(list, length - 1)
                .SelectMany(t => list.Where(e => !t.Contains(e)),
                    (t1, t2) => t1.Concat(new[] { t2 }));
        }
    }
}