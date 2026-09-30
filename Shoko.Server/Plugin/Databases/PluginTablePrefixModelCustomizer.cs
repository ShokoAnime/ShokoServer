using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Shoko.Server.Plugin.Databases;

/// <summary>
/// Builds a plugin context's model with its tables prefixed, so its queries
/// reach the tables its migrations created in the core's database.
/// </summary>
/// <param name="dependencies">The customizer's dependencies.</param>
internal sealed class PluginTablePrefixModelCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
{
    /// <summary>
    /// Builds the model the context describes, then prefixes its tables.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="context">The context.</param>
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);
        if (context.GetService<IDbContextOptions>().FindExtension<PluginTablePrefixExtension>() is { } extension)
            PluginTablePrefixExtension.PrefixTables(modelBuilder.Model, extension.Naming);
    }
}
