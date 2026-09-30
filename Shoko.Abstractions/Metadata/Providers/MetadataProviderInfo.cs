using System;
using System.Collections.Generic;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   Contains information about an <see cref="IMetadataProvider"/>.
/// </summary>
public class MetadataProviderInfo
{
    /// <summary>
    ///   The unique ID of the provider.
    /// </summary>
    /// <remarks>
    ///   Derived from the provider's type and its plugin, so it survives a
    ///   rename and a reinstall. What a client refers to a provider by.
    /// </remarks>
    public required Guid ID { get; init; }

    /// <summary>
    ///   The version of the provider.
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    ///   The display name of the provider.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   Describes what the provider is for.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    ///   The <see cref="IMetadataProvider"/> that this info is for.
    /// </summary>
    public required IMetadataProvider Provider { get; init; }

    /// <summary>
    ///   Information about the configuration that the provider uses.
    /// </summary>
    public required ConfigurationInfo? ConfigurationInfo { get; init; }

    /// <summary>
    ///   Information about the plugin that the provider belongs to.
    /// </summary>
    public required LocalPluginInfo PluginInfo { get; init; }

    /// <summary>
    ///   Whether the provider supplies series, seasons and episodes.
    /// </summary>
    /// <remarks>
    ///   Stated rather than left to a type check, a client having no instance
    ///   to test.
    /// </remarks>
    public required bool SupportsSeries { get; init; }

    /// <summary>
    ///   Whether the provider supplies movies.
    /// </summary>
    /// <remarks>
    ///   Stated rather than left to a type check, a client having no instance
    ///   to test.
    /// </remarks>
    public required bool SupportsMovies { get; init; }

    /// <summary>
    ///   Whether the provider supplies collections.
    /// </summary>
    /// <remarks>
    ///   Stated rather than left to a type check, a client having no instance
    ///   to test.
    /// </remarks>
    public required bool SupportsCollections { get; init; }

    /// <summary>
    ///   Whether the provider can work out what an anime is on its own (and
    ///   preview what it would link), so it can be made the
    ///   <see cref="IsAutoLinker"/>.
    /// </summary>
    /// <remarks>
    ///   Stated rather than left to a type check, a client having no instance
    ///   to test.
    /// </remarks>
    public required bool SupportsAutoLinking { get; init; }

    /// <summary>
    ///   Whether the provider looks series or films up by ID, through
    ///   <see cref="IMetadataSeriesLinkingProvider.LookupSeries"/> or
    ///   <see cref="IMetadataMovieLinkingProvider.LookupMovie"/>.
    /// </summary>
    /// <remarks>
    ///   Worked out at registration from whether the provider's own type
    ///   implements either member, rather than leaving it to the default.
    /// </remarks>
    public bool SupportsLookup { get; init; }

    /// <summary>
    ///   Whether the provider supplies images, through
    ///   <see cref="IMetadataImageProvider"/>.
    /// </summary>
    public bool SupportsImages { get; init; }

    /// <summary>
    ///   The source the provider answers for.
    /// </summary>
    /// <remarks>
    ///   Read from <see cref="IMetadataProvider.Source"/> once, at
    ///   registration, and the same instance. This is what the core routes and
    ///   checks against from then on, so a provider cannot change what it
    ///   answers for while running.
    /// </remarks>
    public required MetadataSource Source { get; init; }

    /// <summary>
    ///   How many of the core's jobs for the provider may run at once, or
    ///   <see langword="null"/> for no limit of its own.
    /// </summary>
    /// <remarks>
    ///   Read from <see cref="IMetadataProvider.MaxConcurrentJobs"/> once, at
    ///   registration. Each of the provider's job types is limited to it.
    /// </remarks>
    public int? MaxConcurrentJobs { get; init; }

    /// <summary>
    ///   Whether the provider can report that it cannot take work right now,
    ///   through <see cref="IPausableMetadataProvider"/>.
    /// </summary>
    public bool SupportsPausing { get; init; }

    /// <summary>
    ///   The entity types this provider can answer for, worked out from the
    ///   shapes it implements.
    /// </summary>
    public required IReadOnlySet<MetadataEntityType> AvailableEntityTypes { get; init; }

    /// <summary>
    ///   The entity types the provider is turned on for, on
    ///   <see cref="Source"/>. Empty while it is off.
    /// </summary>
    /// <remarks>
    ///   A provider starts on for every type it can serve that no earlier
    ///   provider on the source was given; an admin decides from then on.
    ///   <c>series</c>, <c>season</c> and <c>episode</c> each let it take links
    ///   at that level and refresh the series they point into (whole).
    ///   <c>movie</c> and <c>collection</c> let it link and refresh those.
    /// </remarks>
    public required IReadOnlySet<MetadataEntityType> EnabledEntityTypes { get; set; }

    /// <summary>
    ///   Whether the provider answers at all. One with nothing turned on is
    ///   off.
    /// </summary>
    public bool Enabled => EnabledEntityTypes.Count > 0;

    /// <summary>
    ///   Whether the provider is the one that works out what an anime is on
    ///   <see cref="Source"/>, both when a person asks and on its own.
    /// </summary>
    /// <remarks>
    ///   Only one provider is a source's auto-linker, and only while a
    ///   provider taking series or film links for the source is enabled. The
    ///   first provider able to auto-link that registers for a source takes
    ///   it, once, and keeps it: one installed later never takes over by
    ///   itself, and an admin's choice is kept.
    /// </remarks>
    public bool IsAutoLinker { get; set; }

    /// <summary>
    ///   Whether the provider also links new anime and the library sweep by
    ///   itself, rather than only when a person asks. Never set unless
    ///   <see cref="IsAutoLinker"/> is.
    /// </summary>
    public bool AutoLink { get; set; }

    /// <summary>
    ///   Whether restricted entries are fair game for the provider's
    ///   auto-linking. Never set unless <see cref="IsAutoLinker"/> is.
    /// </summary>
    public bool AutoLinkRestricted { get; set; }
}
