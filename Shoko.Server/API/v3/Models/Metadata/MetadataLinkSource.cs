using System;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.API.v3.Models.Metadata;

/// <summary>
/// A source series or movies can be linked to, one row per source, as a link
/// picker lists it.
/// </summary>
public class MetadataLinkSource
{
    /// <summary>
    /// Describes a source's linking as its providers offer it now.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="supportsSeries">Whether a provider of the source links series.</param>
    /// <param name="supportsMovies">Whether a provider of the source links movies.</param>
    /// <param name="isSeriesEnabled">Whether an enabled provider of the source links series.</param>
    /// <param name="isMovieEnabled">Whether an enabled provider of the source links movies.</param>
    /// <param name="status">Whether the source is configured and paused.</param>
    /// <param name="hasIcon">Whether the source has an icon.</param>
    /// <param name="pluginID">The plugin of the source's first registered provider.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="status"/> is <c>null</c>.</exception>
    public MetadataLinkSource(
        MetadataSource source,
        bool supportsSeries,
        bool supportsMovies,
        bool isSeriesEnabled,
        bool isMovieEnabled,
        MetadataSourceStatus status,
        bool hasIcon,
        Guid pluginID
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(status);

        Source = source;
        Name = source.Name;
        SupportsSeries = supportsSeries;
        SupportsMovies = supportsMovies;
        IsSeriesEnabled = isSeriesEnabled;
        IsMovieEnabled = isMovieEnabled;
        Status = status;
        HasIcon = hasIcon;
        PluginID = pluginID;
    }

    /// <summary>
    /// The source, as the link routes take it.
    /// </summary>
    [Required]
    public MetadataSource Source { get; init; }

    /// <summary>
    /// The source's display name.
    /// </summary>
    [Required]
    public string Name { get; init; }

    /// <summary>
    /// The plugin of the source's first registered provider, the core's for
    /// the sources the core serves.
    /// </summary>
    [Required]
    public Guid PluginID { get; init; }

    /// <summary>
    /// Whether the source has an icon, served at
    /// <c>Metadata/Source/{source}/Icon</c>.
    /// </summary>
    [Required]
    public bool HasIcon { get; init; }

    /// <summary>
    /// Whether a provider of the source links series, enabled or not.
    /// </summary>
    [Required]
    public bool SupportsSeries { get; init; }

    /// <summary>
    /// Whether a provider of the source links movies, enabled or not.
    /// </summary>
    [Required]
    public bool SupportsMovies { get; init; }

    /// <summary>
    /// Whether an enabled provider of the source links series, so its search
    /// and lookup answer for them.
    /// </summary>
    [Required]
    public bool IsSeriesEnabled { get; init; }

    /// <summary>
    /// Whether an enabled provider of the source links movies, so its search
    /// and lookup answer for them.
    /// </summary>
    [Required]
    public bool IsMovieEnabled { get; init; }

    /// <summary>
    /// Whether the source is configured, and whether it is paused.
    /// </summary>
    [Required]
    public MetadataSourceStatus Status { get; init; }
}
