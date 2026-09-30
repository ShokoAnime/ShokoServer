using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// The entries a Shoko series is linked to on one source.
/// </summary>
public class LinkedSeriesMetadata
{
    /// <summary>
    /// The linked series, in link order.
    /// </summary>
    [Required]
    public IReadOnlyList<LinkedMetadataEntry> Series { get; init; } = [];

    /// <summary>
    /// The linked movies, whether they claim the whole anime or stand for one
    /// of its episodes.
    /// </summary>
    [Required]
    public IReadOnlyList<LinkedMetadataEntry> Movies { get; init; } = [];
}

/// <summary>
/// The entries a Shoko episode is linked to on one source.
/// </summary>
public class LinkedEpisodeMetadata
{
    /// <summary>
    /// The linked episodes, in link order.
    /// </summary>
    [Required]
    public IReadOnlyList<LinkedMetadataEntry> Episodes { get; init; } = [];

    /// <summary>
    /// The linked movies the episode stands for.
    /// </summary>
    [Required]
    public IReadOnlyList<LinkedMetadataEntry> Movies { get; init; } = [];
}
