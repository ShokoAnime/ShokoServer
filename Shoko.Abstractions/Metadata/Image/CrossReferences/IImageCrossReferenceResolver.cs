using System;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Image.CrossReferences;

/// <summary>
///   A plugin-registered resolver for image cross-references. Plugins implement
///   this interface to provide entity resolution for custom entity types (e.g.,
///   <see cref="DataEntityType.Library"/>) that the core doesn't know about.
/// </summary>
public interface IImageCrossReferenceResolver
{
    /// <summary>
    ///   The name of the resolver, typically matching the plugin name.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   Resolve an entity from its source, type, and stringified identifier.
    ///   This is the inverse of <see cref="TryGetMetadataForEntity"/> and is
    ///   useful for looking up entities when only their metadata triplet is
    ///   available (e.g. from API route parameters or cross-reference data).
    /// </summary>
    /// <param name="entitySource">
    ///   The source of the entity.
    /// </param>
    /// <param name="entityType">
    ///   The type of the entity.
    /// </param>
    /// <param name="entityID">
    ///   The stringified identifier of the entity.
    /// </param>
    /// <returns>
    ///   The resolved entity, or <c>null</c> if not found.
    /// </returns>
    IWithImages? GetEntity(DataSource entitySource, DataEntityType entityType, string entityID);

    /// <summary>
    ///   Try to get the entity metadata (source, type, ID, and optional
    ///   season/episode/date info) from an entity. This is used to create image
    ///   cross-references for custom entity types.
    /// </summary>
    /// <param name="entity">
    ///   The entity to extract metadata from.
    /// </param>
    /// <param name="entitySource">
    ///   The source of the entity.
    /// </param>
    /// <param name="entityType">
    ///   The type of the entity.
    /// </param>
    /// <param name="entityID">
    ///   The ID of the entity.
    /// </param>
    /// <param name="entitySeasonNumber">
    ///   The season number of the entity, if applicable.
    /// </param>
    /// <param name="entityEpisodeNumber">
    ///   The episode number of the entity, if applicable.
    /// </param>
    /// <param name="releasedAt">
    ///   The release date of the entity, if applicable.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the metadata was found, otherwise <c>false</c>.
    /// </returns>
    bool TryGetMetadataForEntity(
        IWithImages entity,
        out DataSource entitySource,
        out DataEntityType entityType,
        [NotNullWhen(true)] out string? entityID,
        out int? entitySeasonNumber,
        out int? entityEpisodeNumber,
        out DateOnly? releasedAt
    );
}
