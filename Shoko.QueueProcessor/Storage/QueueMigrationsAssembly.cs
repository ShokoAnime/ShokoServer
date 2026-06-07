using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#pragma warning disable EF1001 // MigrationsAssembly is EF Core internal API — intentional subclass for migration discovery re-routing
using Microsoft.EntityFrameworkCore.Migrations.Internal;

namespace Shoko.QueueProcessor.Storage;

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
internal sealed class QueueMigrationsAssembly : MigrationsAssembly
{
    private IReadOnlyDictionary<string, TypeInfo>? _migrations;

    public QueueMigrationsAssembly(
        ICurrentDbContext currentContext,
        IDbContextOptions options,
        IMigrationsIdGenerator idGenerator,
        IDiagnosticsLogger<DbLoggerCategory.Migrations> logger)
        : base(currentContext, options, idGenerator, logger) { }

    public override IReadOnlyDictionary<string, TypeInfo> Migrations
        => LazyInitializer.EnsureInitialized(ref _migrations, GetQueueMigrations)!;

    private IReadOnlyDictionary<string, TypeInfo> GetQueueMigrations()
        => Assembly.DefinedTypes
            .Where(t =>
                t.IsSubclassOf(typeof(Migration))
                && t.GetCustomAttribute<DbContextAttribute>()?.ContextType == typeof(SqliteQueueDbContext))
            .OrderBy(t => t.GetCustomAttribute<MigrationAttribute>()?.Id)
            .ToDictionary(
                t => t.GetCustomAttribute<MigrationAttribute>()!.Id,
                t => t,
                StringComparer.OrdinalIgnoreCase);
}
