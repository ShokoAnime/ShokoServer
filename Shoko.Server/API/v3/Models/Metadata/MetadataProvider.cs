using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Configuration;
using Shoko.Server.API.v3.Models.Plugin;

namespace Shoko.Server.API.v3.Models.Metadata;

/// <summary>
/// A metadata provider: what it answers for, and how it is set up.
/// </summary>
public class MetadataProvider
{
    /// <summary>
    /// Describes a provider as it is registered now.
    /// </summary>
    /// <param name="info">The provider's registration.</param>
    /// <param name="status">The pause status of the provider's source.</param>
    /// <param name="providers">Every registered provider, to tell whether the source is configured.</param>
    /// <param name="hasIcon">Whether the provider's source has an icon.</param>
    /// <exception cref="ArgumentNullException"><paramref name="info"/>, <paramref name="status"/> or <paramref name="providers"/> is <c>null</c>.</exception>
    public MetadataProvider(MetadataProviderInfo info, MetadataProviderPauseStatus status, IEnumerable<MetadataProviderInfo> providers, bool hasIcon)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(status);

        ID = info.ID;
        Name = info.Name;
        Description = info.Description ?? string.Empty;
        Version = info.Version;
        Source = info.Source;
        Plugin = new(info.PluginInfo);
        PluginID = info.PluginInfo.ID;
        HasIcon = hasIcon;
        Configuration = info.ConfigurationInfo is null ? null : new(info.ConfigurationInfo);
        SupportsSeries = info.SupportsSeries;
        SupportsMovies = info.SupportsMovies;
        SupportsCollections = info.SupportsCollections;
        SupportsImages = info.SupportsImages;
        SupportsAutoLinking = info.SupportsAutoLinking;
        SupportsLookup = info.SupportsLookup;
        SupportsPausing = info.SupportsPausing;
        AvailableEntityTypes = [.. info.AvailableEntityTypes.Order()];
        EnabledEntityTypes = [.. info.EnabledEntityTypes.Order()];
        IsEnabled = info.Enabled;
        IsConfigured = info.Provider.IsConfigured;
        NotConfiguredReason = IsConfigured ? null : info.Provider.NotConfiguredReason;
        IsAutoLinker = info.IsAutoLinker;
        AutoLink = info.AutoLink;
        AutoLinkRestricted = info.AutoLinkRestricted;
        MaxConcurrentJobs = info.MaxConcurrentJobs;
        Status = new(info.Source, status, providers);
    }

    #region Identity

    /// <summary>
    /// The provider's unique ID.
    /// </summary>
    [Required]
    public Guid ID { get; init; }

    /// <summary>
    /// The provider's display name.
    /// </summary>
    [Required]
    public string Name { get; init; }

    /// <summary>
    /// What the provider is for, or an empty string.
    /// </summary>
    [Required]
    public string Description { get; init; }

    /// <summary>
    /// The provider's version.
    /// </summary>
    [Required]
    public Version Version { get; init; }

    /// <summary>
    /// The source the provider answers for.
    /// </summary>
    [Required]
    public MetadataSource Source { get; init; }

    /// <summary>
    /// The plugin the provider belongs to.
    /// </summary>
    [Required]
    public PluginInfo Plugin { get; init; }

    /// <summary>
    /// The ID of the plugin the provider belongs to.
    /// </summary>
    [Required]
    public Guid PluginID { get; init; }

    /// <summary>
    /// Whether the provider's source has an icon, served at
    /// <c>Metadata/Source/{source}/Icon</c>, and at
    /// <c>Metadata/Provider/{providerID}/Icon</c> unless the provider has one
    /// of its own.
    /// </summary>
    [Required]
    public bool HasIcon { get; init; }

    /// <summary>
    /// The configuration the provider uses, if any.
    /// </summary>
    public ConfigurationInfo? Configuration { get; init; }

    #endregion

    #region Capabilities

    /// <summary>
    /// Whether the provider answers for series.
    /// </summary>
    [Required]
    public bool SupportsSeries { get; init; }

    /// <summary>
    /// Whether the provider answers for movies.
    /// </summary>
    [Required]
    public bool SupportsMovies { get; init; }

    /// <summary>
    /// Whether the provider answers for collections.
    /// </summary>
    [Required]
    public bool SupportsCollections { get; init; }

    /// <summary>
    /// Whether the provider offers images.
    /// </summary>
    [Required]
    public bool SupportsImages { get; init; }

    /// <summary>
    /// Whether the provider can work out on its own what an anime is, and so
    /// show what it would auto-link without linking it.
    /// </summary>
    [Required]
    public bool SupportsAutoLinking { get; init; }

    /// <summary>
    /// Whether the provider looks a single entry up by its ID, for
    /// <c>Metadata/{source}/Search/{kind}/{id}</c>.
    /// </summary>
    [Required]
    public bool SupportsLookup { get; init; }

    /// <summary>
    /// Whether the provider can be paused, and says so in its status.
    /// </summary>
    [Required]
    public bool SupportsPausing { get; init; }

    /// <summary>
    /// The kinds of entries the provider can answer for.
    /// </summary>
    [Required]
    public IReadOnlyList<MetadataEntityType> AvailableEntityTypes { get; init; }

    /// <summary>
    /// How many of the provider's jobs may run at once, if it sets a limit.
    /// </summary>
    public int? MaxConcurrentJobs { get; init; }

    #endregion

    #region Settings

    /// <summary>
    /// Whether the provider is on for at least one kind of entry.
    /// </summary>
    [Required]
    public bool IsEnabled { get; init; }

    /// <summary>
    /// The kinds of entries the provider is on for.
    /// </summary>
    [Required]
    public IReadOnlyList<MetadataEntityType> EnabledEntityTypes { get; init; }

    /// <summary>
    /// Whether the provider has what it needs to answer, such as an API key.
    /// Auto-linking is skipped while it does not, and searches through it
    /// answer <c>503 Service Unavailable</c>.
    /// </summary>
    [Required]
    public bool IsConfigured { get; init; }

    /// <summary>
    /// What the provider is missing, when it is not configured and says.
    /// </summary>
    public string? NotConfiguredReason { get; init; }

    /// <summary>
    /// Whether this is the provider that works out what an anime is for its
    /// source.
    /// </summary>
    [Required]
    public bool IsAutoLinker { get; init; }

    /// <summary>
    /// Whether the source links new anime on its own.
    /// </summary>
    [Required]
    public bool AutoLink { get; init; }

    /// <summary>
    /// Whether the source's automatic links may point at restricted entries.
    /// </summary>
    [Required]
    public bool AutoLinkRestricted { get; init; }

    /// <summary>
    /// Whether the source is paused now, and for how long.
    /// </summary>
    [Required]
    public MetadataSourceStatus Status { get; init; }

    #endregion
}

/// <summary>
/// Whether a metadata source is configured, and whether it is paused and for
/// how long.
/// </summary>
public class MetadataSourceStatus
{
    /// <summary>
    /// Describes a source's status.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="status">Its pause status.</param>
    /// <param name="providers">Every registered provider, to tell whether the source's are configured.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="status"/> or <paramref name="providers"/> is <c>null</c>.</exception>
    public MetadataSourceStatus(MetadataSource source, MetadataProviderPauseStatus status, IEnumerable<MetadataProviderInfo> providers)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(status);

        Source = source;
        (IsConfigured, NotConfiguredReason) = MetadataPauseResponses.GetConfiguration(source, providers);
        IsPaused = status.IsPaused;
        Reason = status.IsPaused ? status.Reason : null;
        ResumesAt = status.IsPaused ? status.ResumesAt?.ToUniversalTime() : null;
        RetryAfterSeconds = status.IsPaused ? MetadataPauseResponses.RetryAfterSeconds(status) : null;
    }

    /// <summary>
    /// The source.
    /// </summary>
    [Required]
    public MetadataSource Source { get; init; }

    /// <summary>
    /// Whether every enabled provider of the source is configured. While the
    /// one a route would talk to is not, the route answers <c>503 Service
    /// Unavailable</c> without a <c>Retry-After</c> header. Not a pause.
    /// </summary>
    [Required]
    public bool IsConfigured { get; init; }

    /// <summary>
    /// What the first enabled provider of the source that is not configured
    /// is missing, when it says.
    /// </summary>
    public string? NotConfiguredReason { get; init; }

    /// <summary>
    /// Whether the source is paused. While it is, the routes that would talk
    /// to its provider answer <c>503 Service Unavailable</c>.
    /// </summary>
    [Required]
    public bool IsPaused { get; init; }

    /// <summary>
    /// Why the source is paused, when it is and says why.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// When the pause ends, in UTC, when it is paused and the end is known.
    /// </summary>
    public DateTime? ResumesAt { get; init; }

    /// <summary>
    /// The seconds left of the pause, as a <c>Retry-After</c> header would
    /// send them, when it is paused.
    /// </summary>
    public int? RetryAfterSeconds { get; init; }
}

/// <summary>
/// What an import of a cross-reference file did.
/// </summary>
public class MetadataImportSummary
{
    /// <summary>
    /// Describes what an import did.
    /// </summary>
    /// <param name="result">The import's result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <c>null</c>.</exception>
    public MetadataImportSummary(MetadataCrossReferenceImportResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        LinkCount = result.LinkCount;
        MoviesAdded = result.MoviesAdded;
        MoviesUpdated = result.MoviesUpdated;
        MoviesKept = result.MoviesKept;
        MoviesRemoved = result.MoviesRemoved;
        SeriesAdded = result.SeriesAdded;
        EpisodesAdded = result.EpisodesAdded;
        EpisodesUpdated = result.EpisodesUpdated;
        EpisodesKept = result.EpisodesKept;
        EpisodesRemoved = result.EpisodesRemoved;
        RefreshesQueued = result.RefreshesQueued;
    }

    /// <summary>
    /// How many links the file held.
    /// </summary>
    [Required]
    public int LinkCount { get; init; }

    /// <summary>
    /// How many movie links were added.
    /// </summary>
    [Required]
    public int MoviesAdded { get; init; }

    /// <summary>
    /// How many movie links had their rating changed.
    /// </summary>
    [Required]
    public int MoviesUpdated { get; init; }

    /// <summary>
    /// How many movie links were already as the file has them.
    /// </summary>
    [Required]
    public int MoviesKept { get; init; }

    /// <summary>
    /// How many movie links the file replaced.
    /// </summary>
    [Required]
    public int MoviesRemoved { get; init; }

    /// <summary>
    /// How many series links were added.
    /// </summary>
    [Required]
    public int SeriesAdded { get; init; }

    /// <summary>
    /// How many episode links were added.
    /// </summary>
    [Required]
    public int EpisodesAdded { get; init; }

    /// <summary>
    /// How many episode links had their rating changed.
    /// </summary>
    [Required]
    public int EpisodesUpdated { get; init; }

    /// <summary>
    /// How many episode links were already as the file has them.
    /// </summary>
    [Required]
    public int EpisodesKept { get; init; }

    /// <summary>
    /// How many episode links the file replaced.
    /// </summary>
    [Required]
    public int EpisodesRemoved { get; init; }

    /// <summary>
    /// How many refreshes of entries not stored yet were queued.
    /// </summary>
    [Required]
    public int RefreshesQueued { get; init; }
}
