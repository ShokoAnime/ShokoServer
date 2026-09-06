using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Repositories;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// Builds a real cached repository whose rows live in an in-memory
/// <see cref="PocoCache{TKey, TEntity}"/> instead of a database.
/// </summary>
/// <remarks>
/// This is deliberately not a mock. <see cref="BaseCachedRepository{T, S}.Cache"/> is a public field
/// and <see cref="ICachedRepository.PopulateIndexes"/> is public, so seeding them directly gives a
/// repository whose real read paths — including the secondary indexes each repository builds for
/// itself — execute exactly as they do in production. Only <c>Save</c>/<c>Delete</c> reach for the
/// (null) database factory, so a test must not call them; use a mock when a write needs observing.
/// </remarks>
public static class CachedRepo
{
    public static TRepo Build<TRepo, TKey, TEntity>(Func<TEntity, TKey> keySelector, params TEntity[] entities)
        where TRepo : BaseCachedRepository<TEntity, TKey>
        where TEntity : class, new()
        where TKey : notnull
        => Build<TRepo, TKey, TEntity>(keySelector, (IEnumerable<TEntity>)entities);

    public static TRepo Build<TRepo, TKey, TEntity>(Func<TEntity, TKey> keySelector, IEnumerable<TEntity>? entities)
        where TRepo : BaseCachedRepository<TEntity, TKey>
        where TEntity : class, new()
        where TKey : notnull
    {
        // Every cached repository stores its constructor arguments without dereferencing them, so
        // nulls are safe for the read-only use this harness supports.
        var constructor = typeof(TRepo).GetConstructors()
            .OrderBy(c => c.GetParameters().Length)
            .First();
        var repository = (TRepo)constructor.Invoke(new object?[constructor.GetParameters().Length]);

        repository.Cache = new PocoCache<TKey, TEntity>(entities ?? [], keySelector);
        repository.PopulateIndexes();

        return repository;
    }

    /// <summary>
    /// Builds a cache-backed repository that also accepts writes, returning the mock so a test can
    /// verify them.
    /// </summary>
    /// <remarks>
    /// A partial mock with <c>CallBase</c> keeps every real read path intact and replaces only the
    /// virtual <c>Save</c>/<c>Delete</c> members, whose real implementations would go to the
    /// database. Writes land in the same <see cref="PocoCache{TKey, TEntity}"/> the reads come from,
    /// so a saved entity is visible to a subsequent lookup exactly as it would be in production.
    /// </remarks>
    public static Mock<TRepo> BuildWritable<TRepo, TKey, TEntity>(Func<TEntity, TKey> keySelector, IEnumerable<TEntity>? entities = null)
        where TRepo : BaseCachedRepository<TEntity, TKey>
        where TEntity : class, new()
        where TKey : notnull
    {
        var constructor = typeof(TRepo).GetConstructors()
            .OrderBy(c => c.GetParameters().Length)
            .First();
        var mock = new Mock<TRepo>(new object[constructor.GetParameters().Length]) { CallBase = true };
        var repository = mock.Object;

        repository.Cache = new PocoCache<TKey, TEntity>(entities ?? [], keySelector);
        repository.PopulateIndexes();

        mock.Setup(r => r.Save(It.IsAny<TEntity>())).Callback<TEntity>(entity => repository.Cache.Update(entity));
        mock.Setup(r => r.Delete(It.IsAny<TEntity>())).Callback<TEntity>(entity => repository.Cache.Remove(entity));

        return mock;
    }
}
