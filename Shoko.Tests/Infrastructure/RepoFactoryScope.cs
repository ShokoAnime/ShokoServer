using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shoko.Server.Repositories;
using Xunit;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// Temporarily installs repositories into <see cref="RepoFactory"/>'s static fields, restoring the
/// previous values on dispose.
/// </summary>
/// <remarks>
/// The domain models resolve their navigation properties through these statics (for example
/// <c>AnimeSeries.AniDB_Anime</c> and <c>AnimeEpisode.VideoLocals</c>), so populating them is what
/// lets a test exercise real model and service code with no database. The fields are plain
/// assignable statics — unlike <c>ISystemService.StaticServices</c>, which is write-once — but they
/// are process-global, so every test using this type must join
/// <see cref="RepoFactoryCollection"/> to keep those mutations serialised.
/// </remarks>
public sealed class RepoFactoryScope : IDisposable
{
    private static readonly FieldInfo[] s_fields = typeof(RepoFactory)
        .GetFields(BindingFlags.Public | BindingFlags.Static);

    private readonly List<(FieldInfo Field, object? Previous)> _saved = [];

    /// <summary>
    /// Installs an already-built repository into the matching <see cref="RepoFactory"/> field.
    /// </summary>
    public RepoFactoryScope Set<TRepo>(TRepo repository) where TRepo : class
    {
        var field = s_fields.Single(f => f.FieldType == typeof(TRepo));
        _saved.Add((field, field.GetValue(null)));
        field.SetValue(null, repository);
        return this;
    }

    /// <summary>
    /// Builds a cache-backed repository from <paramref name="entities"/> and installs it.
    /// </summary>
    public RepoFactoryScope With<TRepo, TKey, TEntity>(Func<TEntity, TKey> keySelector, IEnumerable<TEntity>? entities = null)
        where TRepo : BaseCachedRepository<TEntity, TKey>
        where TEntity : class, new()
        where TKey : notnull
        => Set(CachedRepo.Build<TRepo, TKey, TEntity>(keySelector, entities));

    public void Dispose()
    {
        // Restore in reverse so repeated Set calls for one field unwind to the original value.
        for (var i = _saved.Count - 1; i >= 0; i--)
            _saved[i].Field.SetValue(null, _saved[i].Previous);

        _saved.Clear();
    }
}

/// <summary>
/// Serialises every test that mutates the process-global <see cref="RepoFactory"/> statics. Tests
/// outside this collection keep running in parallel.
/// </summary>
[CollectionDefinition(nameof(RepoFactoryCollection), DisableParallelization = true)]
public sealed class RepoFactoryCollection;
