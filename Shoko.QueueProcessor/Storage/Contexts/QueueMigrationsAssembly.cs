using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Shoko.QueueProcessor.Storage.Contexts;

/// <summary>
/// Custom <see cref="IMigrationsAssembly"/> that returns migrations tagged for
/// <see cref="SqliteQueueDbContext"/> regardless of the current provider context type.
/// <para>
/// EF Core's default implementation filters migrations by exact DbContext type match
/// (<c>[DbContext(typeof(T))]</c> must equal the runtime context type). Since all
/// migrations are generated against <see cref="SqliteQueueDbContext"/>, non-SQLite contexts
/// would find zero migrations and silently skip schema creation. This override re-routes the
/// lookup to always match <see cref="SqliteQueueDbContext"/>-tagged migrations, while each
/// provider's SQL generator still produces provider-appropriate DDL from the untyped columns.
/// </para>
/// </summary>
internal sealed class QueueMigrationsAssembly : IMigrationsAssembly
{
    private readonly Lazy<IReadOnlyDictionary<string, TypeInfo>> _migrations = new(BuildMigrations);
    private readonly Lazy<ModelSnapshot?> _modelSnapshot = new(BuildModelSnapshot);

    public Assembly Assembly => typeof(SqliteQueueDbContext).Assembly;

    public ModelSnapshot? ModelSnapshot => _modelSnapshot.Value;

    public IReadOnlyDictionary<string, TypeInfo> Migrations => _migrations.Value;

    public string? FindMigrationId(string nameOrId)
    {
        var id = nameOrId.TrimStart('[').TrimEnd(']');
        return Migrations.Keys.FirstOrDefault(k =>
            string.Equals(k, id, StringComparison.OrdinalIgnoreCase) ||
            k.EndsWith("_" + id, StringComparison.OrdinalIgnoreCase));
    }

    public Migration CreateMigration(TypeInfo migrationClass, string activeProvider)
    {
        var migration = (Migration)Activator.CreateInstance(migrationClass.AsType())!;
        migration.ActiveProvider = activeProvider;
        return migration;
    }

    private static ModelSnapshot? BuildModelSnapshot()
        => typeof(SqliteQueueDbContext).Assembly.GetExportedTypes()
            .Where(t =>
                t.IsSubclassOf(typeof(ModelSnapshot)) &&
                t.GetCustomAttribute<DbContextAttribute>()?.ContextType == typeof(SqliteQueueDbContext))
            .FirstOrDefault()
            ?.GetConstructor([])
            ?.Invoke(null) as ModelSnapshot;

    private static IReadOnlyDictionary<string, TypeInfo> BuildMigrations()
        => typeof(SqliteQueueDbContext).Assembly.DefinedTypes
            .Where(t =>
                t.IsSubclassOf(typeof(Migration)) &&
                t.GetCustomAttribute<MigrationAttribute>() is not null &&
                t.GetCustomAttribute<DbContextAttribute>()?.ContextType == typeof(SqliteQueueDbContext))
            .OrderBy(t => t.GetCustomAttribute<MigrationAttribute>()!.Id)
            .ToDictionary(
                t => t.GetCustomAttribute<MigrationAttribute>()!.Id,
                t => t,
                StringComparer.OrdinalIgnoreCase);
}
