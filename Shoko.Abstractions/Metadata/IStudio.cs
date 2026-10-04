using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// A studio.
/// </summary>
public interface IStudio : IMetadata, IWithPrimaryImage
{
    /// <summary>
    /// The name of the studio.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The original name of the studio.
    /// </summary>
    string? OriginalName { get; }

    /// <summary>
    ///   When the core last asked the source to refresh the studio, found or
    ///   not, in UTC. Set by the core alone; <c>null</c> when it
    ///   never did.
    /// </summary>
    DateTime? LastRefreshedAt { get; }

    /// <summary>
    /// The type of studio.
    /// </summary>
    StudioType StudioType { get; }

    /// <summary>
    ///   The country the studio originates from, as the source gives it,
    ///   usually an ISO 3166-1 code such as <c>JP</c>, or <c>null</c> when
    ///   the source does not say.
    /// </summary>
    string? CountryOfOrigin { get; }

    /// <summary>
    /// All locally known movie works by the studio.
    /// </summary>
    IEnumerable<IMovie> MovieWorks { get; }

    /// <summary>
    /// All locally known series works by the studio.
    /// </summary>
    IEnumerable<ISeries> SeriesWorks { get; }

    /// <summary>
    /// All locally known works by the studio.
    /// </summary>
    IEnumerable<IMetadata> Works { get; }
}

/// <summary>
/// A studio for a parent entity.
/// </summary>
public interface IStudio<TMetadata> : IStudio where TMetadata : IMetadata
{
    /// <summary>
    ///   The entry the studio worked on, which may be a series or a film.
    /// </summary>
    MetadataGuid ParentID { get; }

    /// <summary>
    /// Parent metadata entity.
    /// </summary>
    TMetadata? Parent { get; }
}
