using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Shoko.Abstractions.Extensions;

/// <summary>
///   Extension methods for lists.
/// </summary>
public static class ListExtensions
{
    /// <summary>
    /// Attempts to remove an item from the list at the given index.
    /// </summary>
    /// <param name="list">The list to remove the item from.</param>
    /// <param name="index">The index of the item to remove.</param>
    /// <param name="item">The item to remove.</param>
    /// <returns><c>true</c> if the item was removed, <c>false</c> if the index is out of range.</returns>
    public static bool TryRemoveAt<T>(this IList<T> list, int index, [NotNullWhen(true)] out T? item)
    {
        if (index < 0 || index >= list.Count)
        {
            item = default;
            return false;
        }
        item = list[index]!;
        list.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Returns a range of items from the list.
    /// </summary>
    /// <param name="list">The list to get the range from.</param>
    /// <param name="start">The index of the first item to return.</param>
    /// <param name="end">The index of the last item to return.</param>
    /// <returns>An enumerable containing the items in the range.</returns>
    public static IEnumerable<T> GetRange<T>(this IList<T> list, int start, int end)
    {
        if (start < 0 || start >= list.Count)
            yield break;

        for (var index = 0; index < end - start; index++)
        {
            yield return list[start + index];
        }
    }

    /// <summary>
    /// Finds the index of the first item in the list which matches the given condition.
    /// </summary>
    /// <param name="list">The list to search.</param>
    /// <param name="item">The condition to match.</param>
    /// <returns>The index of the first matching item, or -1 if no item matches.</returns>
    public static int FindIndex<T>(this IList<T> list, Func<T, bool> item)
    {
        var index = 0;
        foreach (var element in list)
        {
            if (item(element))
                return index;
            index++;
        }

        return -1;
    }

    /// <summary>
    /// Finds the index of the given item in the list.
    /// </summary>
    /// <param name="list">The list to search.</param>
    /// <param name="item">The item to search for.</param>
    /// <returns>The index of the item, or -1 if the item is not in the list.</returns>
    public static int IndexOf<T>(this IReadOnlyList<T> list, T item)
    {
        var index = 0;
        foreach (var element in list)
        {
            if (element is null ? item is null : element.Equals(item))
                return index;
            index++;
        }
        return -1;
    }

    /// <summary>
    /// Finds the index of the first item in the list which matches the given condition.
    /// </summary>
    /// <param name="list">The list to search.</param>
    /// <param name="item">The condition to match.</param>
    /// <returns>The index of the first matching item, or -1 if no item matches.</returns>
    public static T? Find<T>(this IReadOnlyList<T> list, Func<T, bool> item)
    {
        var index = 0;
        foreach (var element in list)
        {
            if (item(element))
                return element;
            index++;
        }

        return default;
    }

    /// <summary>
    /// Finds the index of the first item in the list which matches the given condition.
    /// </summary>
    /// <param name="list">The list to search.</param>
    /// <param name="item">The condition to match.</param>
    /// <returns>The index of the first matching item, or -1 if no item matches.</returns>
    public static int FindIndex<T>(this IReadOnlyList<T> list, Func<T, bool> item)
    {
        var index = 0;
        foreach (var element in list)
        {
            if (item(element))
                return index;
            index++;
        }

        return -1;
    }

    /// <summary>
    /// Returns a range of items from the list.
    /// </summary>
    /// <param name="list">The list to get the range from.</param>
    /// <param name="start">The index of the first item to return.</param>
    /// <param name="end">The index of the last item to return.</param>
    /// <returns>An enumerable containing the items in the range.</returns>
    public static IEnumerable<T> GetRange<T>(this IReadOnlyList<T> list, int start, int end)
    {
        if (start < 0 || start >= list.Count)
            yield break;

        for (var index = 0; index < end - start; index++)
        {
            yield return list[start + index];
        }
    }
}

