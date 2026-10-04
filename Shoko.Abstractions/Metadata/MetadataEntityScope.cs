using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   A set of source and kind pairs, each naming the entries of one kind on
///   one source, such as the series on <c>tmdb</c>. What a metadata resolver
///   resolves and what an image contributor adds images for.
/// </summary>
/// <remarks>
///   Immutable. Build one with <see cref="Single"/> for one pair,
///   <see cref="ForSource"/> for one source and some kinds,
///   <see cref="ForSources"/> for every kind listed on every source listed,
///   or <see cref="FromPairs"/> for any pairs. Enumerates its pairs ordered
///   by source and then by kind.
/// </remarks>
public sealed class MetadataEntityScope : IReadOnlyCollection<(MetadataSource Source, MetadataEntityType EntityType)>, IEquatable<MetadataEntityScope>
{
    #region Fields

    private readonly FrozenSet<(MetadataSource Source, MetadataEntityType EntityType)> _pairs;

    private readonly (MetadataSource Source, MetadataEntityType EntityType)[] _ordered;

    private readonly FrozenDictionary<MetadataSource, FrozenSet<MetadataEntityType>> _bySource;

    private readonly FrozenSet<MetadataSource> _sources;

    #endregion

    #region Constructors

    private MetadataEntityScope(IEnumerable<(MetadataSource Source, MetadataEntityType EntityType)> pairs)
    {
        _pairs = pairs.ToFrozenSet();
        _ordered = [.. _pairs.OrderBy(pair => pair.Source).ThenBy(pair => pair.EntityType)];
        _bySource = _pairs
            .GroupBy(pair => pair.Source)
            .ToFrozenDictionary(group => group.Key, group => group.Select(pair => pair.EntityType).ToFrozenSet());
        _sources = _bySource.Keys.ToFrozenSet();
    }

    /// <summary>
    ///   The scope holding no pair.
    /// </summary>
    public static MetadataEntityScope Empty { get; } = new([]);

    /// <summary>
    ///   A scope of one kind on one source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="entityType">The kind.</param>
    /// <returns>The scope.</returns>
    /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
    public static MetadataEntityScope Single(MetadataSource source, MetadataEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(entityType);
        return new([(source, entityType)]);
    }

    /// <summary>
    ///   A scope of some kinds on one source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="entityTypes">The kinds; repeats are kept once.</param>
    /// <returns>The scope.</returns>
    /// <exception cref="ArgumentNullException">An argument, or one of the kinds, is <c>null</c>.</exception>
    public static MetadataEntityScope ForSource(MetadataSource source, params IEnumerable<MetadataEntityType> entityTypes)
    {
        ArgumentNullException.ThrowIfNull(source);
        return ForSources([source], entityTypes);
    }

    /// <summary>
    ///   A scope of every kind listed on every source listed.
    /// </summary>
    /// <param name="sources">The sources; repeats are kept once.</param>
    /// <param name="entityTypes">The kinds; repeats are kept once.</param>
    /// <returns>The scope.</returns>
    /// <exception cref="ArgumentNullException">An argument, or one of the sources or kinds, is <c>null</c>.</exception>
    public static MetadataEntityScope ForSources(IEnumerable<MetadataSource> sources, IEnumerable<MetadataEntityType> entityTypes)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(entityTypes);
        var kinds = entityTypes.ToList();
        return FromPairs(sources.SelectMany(source => kinds.Select(entityType => (source, entityType))));
    }

    /// <summary>
    ///   A scope of the given pairs.
    /// </summary>
    /// <param name="pairs">The pairs; repeats are kept once.</param>
    /// <returns>The scope.</returns>
    /// <exception cref="ArgumentNullException">The pairs, or a source or kind in them, is <c>null</c>.</exception>
    public static MetadataEntityScope FromPairs(IEnumerable<(MetadataSource Source, MetadataEntityType EntityType)> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        var list = pairs.ToList();
        if (list.Any(pair => pair.Source is null || pair.EntityType is null))
            throw new ArgumentNullException(nameof(pairs), "A pair names no source or no kind.");

        return list.Count is 0 ? Empty : new(list);
    }

    #endregion

    #region Reading

    /// <summary>
    ///   How many pairs the scope holds.
    /// </summary>
    public int Count => _pairs.Count;

    /// <summary>
    ///   Whether the scope holds no pair.
    /// </summary>
    public bool IsEmpty => _pairs.Count is 0;

    /// <summary>
    ///   Every source the scope names at least one kind on.
    /// </summary>
    public IReadOnlySet<MetadataSource> Sources => _sources;

    /// <summary>
    ///   The kinds the scope holds on one source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The kinds, or none when the scope does not name the source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public IReadOnlySet<MetadataEntityType> GetEntityTypes(MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return _bySource.TryGetValue(source, out var entityTypes) ? entityTypes : FrozenSet<MetadataEntityType>.Empty;
    }

    /// <summary>
    ///   Whether the scope holds a kind on a source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="entityType">The kind.</param>
    /// <returns><c>true</c> when it does.</returns>
    /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
    public bool Contains(MetadataSource source, MetadataEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(entityType);
        return _pairs.Contains((source, entityType));
    }

    /// <summary>
    ///   Whether the scope holds the source and kind of an entry.
    /// </summary>
    /// <param name="id">The entry.</param>
    /// <returns><c>true</c> when it does.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    public bool Contains(MetadataGuid id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _pairs.Contains((id.Source, id.EntityType));
    }

    #endregion

    #region Set Operations

    /// <summary>
    ///   The pairs held by this scope, the other, or both.
    /// </summary>
    /// <param name="other">The other scope.</param>
    /// <returns>The union.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public MetadataEntityScope Union(MetadataEntityScope other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return FromPairs(_pairs.Concat(other._pairs));
    }

    /// <summary>
    ///   The pairs held by both this scope and the other.
    /// </summary>
    /// <param name="other">The other scope.</param>
    /// <returns>The intersection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public MetadataEntityScope Intersect(MetadataEntityScope other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return FromPairs(_pairs.Where(other._pairs.Contains));
    }

    /// <summary>
    ///   The pairs held by this scope and not by the other.
    /// </summary>
    /// <param name="other">The other scope.</param>
    /// <returns>The difference.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public MetadataEntityScope Except(MetadataEntityScope other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return FromPairs(_pairs.Where(pair => !other._pairs.Contains(pair)));
    }

    #endregion

    #region Enumeration

    /// <inheritdoc/>
    public IEnumerator<(MetadataSource Source, MetadataEntityType EntityType)> GetEnumerator()
        => ((IEnumerable<(MetadataSource Source, MetadataEntityType EntityType)>)_ordered).GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    #endregion

    #region Equality

    /// <inheritdoc/>
    public bool Equals(MetadataEntityScope? other)
        => other is not null && _pairs.SetEquals(other._pairs);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is MetadataEntityScope other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = 0;
        foreach (var pair in _pairs)
            hash ^= pair.GetHashCode();
        return hash;
    }

    /// <summary>
    ///   The pairs as <c>source://kind</c>, joined by commas, e.g.
    ///   <c>tmdb://movie, tmdb://series</c>.
    /// </summary>
    /// <returns>The text.</returns>
    public override string ToString()
        => string.Join(", ", _ordered.Select(pair => $"{pair.Source.Value}://{pair.EntityType.Value}"));

    #endregion
}
