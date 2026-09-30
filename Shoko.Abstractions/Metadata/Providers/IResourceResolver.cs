
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   A plugin's resolver that adds <see cref="Resource"/> entries to
///   <see cref="IWithResources"/> entities, called by
///   <see cref="Services.IMetadataService.GatherResourcesForEntity"/>.
/// </summary>
/// <remarks>
///   Runs while an entity's <see cref="IWithResources.Resources"/> is
///   evaluated. Re-entrance is guarded, so a resolver may read the entity's
///   default properties without recursing.
/// </remarks>
public interface IResourceResolver
{
    /// <summary>
    ///   The name of the resolver, typically matching the plugin name.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   Resolve and return additional <see cref="Resource"/> entries for the
    ///   given entity.
    /// </summary>
    /// <param name="entity">
    ///   The entity to resolve resources for.
    /// </param>
    /// <returns>
    ///   A read-only list of resolved <see cref="Resource"/> entries, or an
    ///   empty list if the resolver has nothing to contribute for this entity.
    /// </returns>
    IReadOnlyList<Resource> Resolve(IWithResources entity);
}
