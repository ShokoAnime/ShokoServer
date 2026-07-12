
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Resources;

/// <summary>
///   A plugin-registered resolver that contributes additional
///   <see cref="Resource"/> entries for <see cref="IWithResources"/> entities.
///   Resolvers are registered with <see cref="Services.IMetadataService"/>
///   via <c>AddParts</c> and are called by
///   <see cref="Services.IMetadataService.GatherResourcesForEntity"/>.
/// </summary>
/// <remarks>
///   Resolvers run during evaluation of
///   <see cref="IWithResources.Resources"/> on an entity. Each registered
///   resolver is queried and its returned resources are aggregated into the
///   final list.  The <c>GatherResourcesForEntity</c> implementation guards
///   against re-entrance, so resolvers may safely access default entity
///   properties without causing infinite recursion.
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
