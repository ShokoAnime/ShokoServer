using System.Collections.Generic;
using System.Threading;

namespace Shoko.Server.Repositories.Cached.Metadata.Text;

/// <summary>
///   The overview values the text cache has read or written lately, bounded by
///   their total length and dropping the least recently used first.
/// </summary>
/// <param name="capacity">How many characters to keep, or <c>null</c> to keep every value.</param>
internal sealed class OverviewBodyCache(long? capacity)
{
    private readonly Lock _lock = new();

    private readonly Dictionary<int, LinkedListNode<(int ID, string Value)>> _nodes = [];

    private readonly LinkedList<(int ID, string Value)> _order = [];

    private long _size;

    /// <summary>
    ///   How many characters are kept.
    /// </summary>
    internal long Size
    {
        get
        {
            lock (_lock)
                return _size;
        }
    }

    /// <summary>
    ///   How many values are kept.
    /// </summary>
    internal int Count
    {
        get
        {
            lock (_lock)
                return _nodes.Count;
        }
    }

    /// <summary>
    ///   Looks up a value, marking it as just used.
    /// </summary>
    /// <param name="id">The overview's ID.</param>
    /// <param name="value">The value, when it is kept.</param>
    /// <returns><c>true</c> when it is kept.</returns>
    internal bool TryGet(int id, out string value)
    {
        lock (_lock)
        {
            if (!_nodes.TryGetValue(id, out var node))
            {
                value = string.Empty;
                return false;
            }

            _order.Remove(node);
            _order.AddFirst(node);
            value = node.Value.Value;
            return true;
        }
    }

    /// <summary>
    ///   Keeps a value, replacing any kept for the same overview, and drops the
    ///   least recently used values past the capacity.
    /// </summary>
    /// <param name="id">The overview's ID.</param>
    /// <param name="value">The value.</param>
    internal void Put(int id, string value)
    {
        lock (_lock)
            PutLocked(id, value);
    }

    /// <summary>
    ///   Keeps a value read from the database unless one is kept already,
    ///   which a write that landed after the read put there.
    /// </summary>
    /// <param name="id">The overview's ID.</param>
    /// <param name="value">The value read.</param>
    /// <returns>The value kept for the overview now.</returns>
    internal string GetOrAdd(int id, string value)
    {
        lock (_lock)
        {
            if (_nodes.TryGetValue(id, out var node))
                return node.Value.Value;

            PutLocked(id, value);
            return value;
        }
    }

    /// <summary>
    ///   Forgets a value.
    /// </summary>
    /// <param name="id">The overview's ID.</param>
    internal void Remove(int id)
    {
        lock (_lock)
            RemoveLocked(id);
    }

    /// <summary>
    ///   Forgets every value.
    /// </summary>
    internal void Clear()
    {
        lock (_lock)
        {
            _nodes.Clear();
            _order.Clear();
            _size = 0;
        }
    }

    private void PutLocked(int id, string value)
    {
        RemoveLocked(id);
        _nodes[id] = _order.AddFirst((id, value));
        _size += value.Length;
        while (capacity is { } limit && _size > limit && _order.Last is { } last && !ReferenceEquals(last, _order.First))
            RemoveLocked(last.Value.ID);
    }

    private void RemoveLocked(int id)
    {
        if (!_nodes.Remove(id, out var node))
            return;

        _order.Remove(node);
        _size -= node.Value.Value.Length;
    }
}
