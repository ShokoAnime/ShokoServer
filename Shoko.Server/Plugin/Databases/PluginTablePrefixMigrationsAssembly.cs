using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Shoko.Server.Plugin.Databases;

/// <summary>
/// Finds a plugin context's migrations the way Entity Framework Core does, by
/// the context type they were generated for, and prefixes every table they,
/// their target models and the snapshot name.
/// </summary>
/// <param name="currentContext">The context being migrated.</param>
/// <param name="options">The context's options, with the naming.</param>
internal sealed class PluginTablePrefixMigrationsAssembly(ICurrentDbContext currentContext, IDbContextOptions options) : IMigrationsAssembly
{
    private readonly Type _contextType = currentContext.Context.GetType();

    private readonly PluginTableNaming _naming = options.FindExtension<PluginTablePrefixExtension>()?.Naming
        ?? throw new InvalidOperationException($"{nameof(PluginTablePrefixMigrationsAssembly)} needs a {nameof(PluginTablePrefixExtension)} in the context's options.");

    private IReadOnlyDictionary<string, TypeInfo>? _migrations;

    private ModelSnapshot? _modelSnapshot;

    private bool _modelSnapshotBuilt;

    /// <summary>
    /// The assembly holding the migrations: the context's own.
    /// </summary>
    public Assembly Assembly => _contextType.Assembly;

    /// <summary>
    /// The context's migrations by ID, oldest first.
    /// </summary>
    public IReadOnlyDictionary<string, TypeInfo> Migrations => _migrations ??= Assembly.DefinedTypes
        .Where(type => type.IsSubclassOf(typeof(Migration)) && !type.IsAbstract && type.GetCustomAttribute<DbContextAttribute>()?.ContextType == _contextType)
        .Select(type => (Type: type, Id: type.GetCustomAttribute<MigrationAttribute>()?.Id))
        .Where(migration => migration.Id is not null)
        .OrderBy(migration => migration.Id, StringComparer.Ordinal)
        .ToDictionary(migration => migration.Id!, migration => migration.Type, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The context's model snapshot, with its tables prefixed.
    /// </summary>
    public ModelSnapshot? ModelSnapshot
    {
        get
        {
            if (_modelSnapshotBuilt)
                return _modelSnapshot;

            var type = Assembly.DefinedTypes
                .FirstOrDefault(type => type.IsSubclassOf(typeof(ModelSnapshot)) && !type.IsAbstract && type.GetCustomAttribute<DbContextAttribute>()?.ContextType == _contextType);
            _modelSnapshot = type is null ? null : (ModelSnapshot)Activator.CreateInstance(type.AsType())!;
            if (_modelSnapshot?.Model is IMutableModel model)
                PluginTablePrefixExtension.PrefixTables(model, _naming);
            _modelSnapshotBuilt = true;
            return _modelSnapshot;
        }
    }

    /// <summary>
    /// Finds a migration by its ID or name.
    /// </summary>
    /// <param name="nameOrId">The migration's ID, or its name without the timestamp.</param>
    /// <returns>The migration's ID, or <see langword="null"/> when there is none.</returns>
    public string? FindMigrationId(string nameOrId)
        => Migrations.Keys.FirstOrDefault(id => string.Equals(id, nameOrId, StringComparison.OrdinalIgnoreCase))
            ?? Migrations.Keys.FirstOrDefault(id => id.EndsWith("_" + nameOrId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Creates a migration with every table it names prefixed.
    /// </summary>
    /// <param name="migrationClass">The migration's type.</param>
    /// <param name="activeProvider">The provider the migration runs on.</param>
    /// <returns>The migration.</returns>
    /// <exception cref="NotSupportedException">The migration has an operation that cannot be moved into the core's database.</exception>
    public Migration CreateMigration(TypeInfo migrationClass, string activeProvider)
    {
        var migration = (Migration)Activator.CreateInstance(migrationClass.AsType())!;
        migration.ActiveProvider = activeProvider;

        // The operations are built once and cached by the migration, so they are renamed in place,
        // and the target model with them, which the SQL generator looks tables up in.
        _naming.Rewrite(migration.UpOperations);
        _naming.Rewrite(migration.DownOperations);
        if (migration.TargetModel is IMutableModel targetModel)
            PluginTablePrefixExtension.PrefixTables(targetModel, _naming);

        return migration;
    }
}
