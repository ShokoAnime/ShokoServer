using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Shoko.Server.Plugin.Databases;

/// <summary>
/// Keeps a plugin context's model per prefix as well as per context type, so
/// one context type never reuses a model built for another prefix.
/// </summary>
internal sealed class PluginTablePrefixModelCacheKeyFactory : IModelCacheKeyFactory
{
    /// <summary>
    /// The key a context's model is cached under.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="designTime">Whether the model is the design-time one.</param>
    /// <returns>The key.</returns>
    public object Create(DbContext context, bool designTime)
        => (context.GetType(), context.GetService<IDbContextOptions>().FindExtension<PluginTablePrefixExtension>()?.Naming.Prefix, designTime);
}
