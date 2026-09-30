using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Shoko.Server.Plugin.Databases;

/// <summary>
/// Carries a plugin database's <see cref="PluginTableNaming"/> in its context
/// options, for the services that prefix its tables to read.
/// </summary>
/// <param name="naming">The naming.</param>
internal sealed class PluginTablePrefixExtension(PluginTableNaming naming) : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;

    /// <summary>
    /// The naming.
    /// </summary>
    public PluginTableNaming Naming { get; } = naming;

    /// <summary>
    /// Information about the extension, for Entity Framework Core.
    /// </summary>
    public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

    /// <summary>
    /// Adds nothing; the prefixing services are replaced on the options.
    /// </summary>
    /// <param name="services">The context's internal services.</param>
    public void ApplyServices(IServiceCollection services) { }

    /// <summary>
    /// Accepts any options.
    /// </summary>
    /// <param name="options">The options.</param>
    public void Validate(IDbContextOptions options) { }

    #region Model

    /// <summary>
    /// Prefixes the table of every entity type in a model still being built:
    /// the context's own, its snapshot, or a migration's target.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="naming">The naming.</param>
    internal static void PrefixTables(IMutableModel model, PluginTableNaming naming)
    {
        // Read every name first, as a derived or owned type shares its table and reads the
        // name through the type it shares it with, which may already be renamed.
        var tables = new List<(IMutableEntityType EntityType, string Table)>();
        foreach (var entityType in model.GetEntityTypes())
        {
            if (entityType.IsMappedToJson() || entityType.GetTableName() is not { } table)
                continue;
            tables.Add((entityType, table));
        }

        foreach (var (entityType, table) in tables)
            entityType.SetTableName(naming.Name(table));
    }

    #endregion

    private sealed class ExtensionInfo(PluginTablePrefixExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => false;

        public override string LogFragment => $"PluginTablePrefix={extension.Naming.Prefix} ";

        // The prefix is read from the options at runtime, so every prefix shares one service provider.
        public override int GetServiceProviderHashCode()
            => 0;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["Shoko:PluginTablePrefix"] = extension.Naming.Prefix;
    }
}
