using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Config;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   Supplies metadata from a source the core does not serve itself.
/// </summary>
/// <remarks>
///   The shared half: implement <see cref="IMetadataSeriesProvider"/>,
///   <see cref="IMetadataMovieProvider"/> or <see cref="IMetadataAutoLinkingProvider"/>
///   too, or the provider is dropped at registration. The core runs the jobs
///   that call you, decides when an entry is due and keeps one refresh of it
///   at a time; you fetch from your source and write into the core's stores.
/// </remarks>
public interface IMetadataProvider
{
    /// <summary>
    ///   The display name, typically matching the plugin's.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   The source you answer for.
    /// </summary>
    /// <remarks>
    ///   The instance <see cref="MetadataSource.Register"/> returned. Read once,
    ///   at registration, and kept on <see cref="MetadataProviderInfo.Source"/>,
    ///   so return a constant. A plugin serving several sources defines one
    ///   provider for each. A source in
    ///   <see cref="Services.IMetadataProviderManager.ReservedSources"/> is
    ///   refused.
    /// </remarks>
    MetadataSource Source { get; }

    /// <summary>
    ///   Describes what the provider is for.
    /// </summary>
    string? Description { get => null; }

    /// <summary>
    ///   The provider's version, defaulting to the assembly's.
    /// </summary>
    Version Version { get => GetType().Assembly.GetName().Version ?? new Version(0, 0, 0, 0); }

    /// <summary>
    ///   Whether you should be left to link anime on your own before anybody
    ///   asks you to.
    /// </summary>
    /// <remarks>
    ///   Only the starting position, read when you claim a source's
    ///   auto-linking; an admin's decision is kept and this is not consulted
    ///   again. The default is <c>true</c>: only anime fetched or
    ///   refreshed from then on are searched, never the whole library at
    ///   once. Return <c>false</c> to wait for a person to ask.
    /// </remarks>
    bool AutoLinkByDefault { get => true; }

    /// <summary>
    ///   Whether restricted entries are included when you link on your own.
    /// </summary>
    /// <remarks>
    ///   Read at the same time, on the same terms, as
    ///   <see cref="AutoLinkByDefault"/>, and means nothing without it.
    /// </remarks>
    bool AutoLinkRestrictedByDefault { get => false; }

    /// <summary>
    ///   The kinds you suggest being turned on for when you are first seen on
    ///   your source. Every kind you serve by default.
    /// </summary>
    /// <remarks>
    ///   Seeded from each provider's own suggestion on first sight, owned by
    ///   the admin afterwards: read once, at registration, for each kind you
    ///   serve that has no decision about you yet, and never again for it. A
    ///   kind decided as nobody keeps you off whatever you suggest. Leave a
    ///   kind out to start off for it, such as the <c>collection</c> kind
    ///   when collections are not worth fetching by default. Kinds you do not
    ///   serve are ignored.
    /// </remarks>
    IReadOnlySet<MetadataEntityType> DefaultEnabledKinds
    {
        get => new HashSet<MetadataEntityType>(
            (this is IMetadataSeriesProvider ? [MetadataEntityType.Series, MetadataEntityType.Season, MetadataEntityType.Episode] : (MetadataEntityType[])[])
                .Concat(this is IMetadataMovieProvider ? [MetadataEntityType.Movie] : [])
                .Concat(this is IMetadataCollectionProvider ? [MetadataEntityType.Collection] : [])
                .Concat(this is IMetadataEntityProvider entityProvider ? entityProvider.EntityScope.Select(pair => pair.EntityType) : [])
        );
    }

    /// <summary>
    ///   Whether you have what you need to answer, such as an API key or
    ///   credentials.
    /// </summary>
    /// <remarks>
    ///   Read every time the core is about to auto-link for you. While it is
    ///   <c>false</c>, auto-linking is skipped quietly: no search
    ///   is queued and a queued one does nothing, and it starts again for the
    ///   anime fetched or refreshed once you are configured. The default is
    ///   <c>true</c>.
    /// </remarks>
    bool IsConfigured { get => true; }

    /// <summary>
    ///   What you are missing while <see cref="IsConfigured"/> is
    ///   <c>false</c>, in words a person can read, such as "No API
    ///   key is set.", or <c>null</c> to give no reason.
    /// </summary>
    /// <remarks>
    ///   Shown in the source's status and in the answer to a search refused
    ///   while you are not configured. Not read while you are configured. The
    ///   default is <c>null</c>.
    /// </remarks>
    string? NotConfiguredReason { get => null; }

    /// <summary>
    ///   How many of the core's jobs for you may run at once, or
    ///   <c>null</c> for no limit of your own.
    /// </summary>
    /// <remarks>
    ///   The core runs your refresh, search and image jobs as job types of
    ///   your own, and this becomes each type's limit. Read once, at
    ///   registration, and kept on
    ///   <see cref="MetadataProviderInfo.MaxConcurrentJobs"/>.
    /// </remarks>
    int? MaxConcurrentJobs { get => null; }

    /// <summary>
    ///   Clean up what you keep of your own for an entry the core purged.
    /// </summary>
    /// <remarks>
    ///   Optional. The core has already removed everything it stores for the
    ///   entry (store rows, texts, credits, tags, studios, relations,
    ///   suggestions, image links and a series' orderings); only what you keep
    ///   yourself, such as rows in your own database, is left. Called once per
    ///   purged entry on every provider claiming the source, even while
    ///   disabled, under the entry's lock.
    /// </remarks>
    /// <param name="entryID">
    ///   The purged series, movie or collection, on <see cref="Source"/>.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes once you have cleaned up.</returns>
    Task CleanUp(MetadataGuid entryID, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>
///   Typed variant of <see cref="IMetadataProvider"/> for providers that
///   expose plugin-level configuration.
/// </summary>
/// <typeparam name="TConfiguration">The configuration's type.</typeparam>
public interface IMetadataProvider<TConfiguration> : IMetadataProvider
    where TConfiguration : class, IConfiguration, new();
