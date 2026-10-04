using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Metadata.Services;

/// <summary>
///   Registers the metadata providers and holds what an admin decided about
///   them: which answers for what, and who auto-links each source.
/// </summary>
public interface IMetadataProviderManager
{
    #region Providers

    /// <summary>
    ///   The metadata providers registered with the service.
    /// </summary>
    IReadOnlyList<MetadataProviderInfo> MetadataProviders { get; }

    /// <summary>
    ///   Looks up a provider by its stable ID.
    /// </summary>
    /// <param name="providerID">The provider's ID.</param>
    /// <returns>
    ///   The info, or <c>null</c> if nothing goes by that ID.
    /// </returns>
    MetadataProviderInfo? GetProviderInfo(Guid providerID);

    /// <summary>
    ///   Looks up a provider's info from the provider itself.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>The info.</returns>
    /// <exception cref="System.ArgumentNullException">
    ///   Thrown when <paramref name="provider"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="System.ArgumentException">
    ///   Thrown when the provider was never registered.
    /// </exception>
    MetadataProviderInfo GetProviderInfo(IMetadataProvider provider);

    /// <summary>
    ///   Looks up a provider's info from its type.
    /// </summary>
    /// <typeparam name="TProvider">The provider's type.</typeparam>
    /// <returns>The info.</returns>
    /// <exception cref="System.ArgumentException">
    ///   Thrown when no provider of that type was registered.
    /// </exception>
    MetadataProviderInfo GetProviderInfo<TProvider>() where TProvider : class, IMetadataProvider;

    /// <summary>
    ///   Looks up a provider's info from its type.
    /// </summary>
    /// <param name="providerType">The provider's type.</param>
    /// <returns>The info.</returns>
    /// <exception cref="System.ArgumentNullException">
    ///   Thrown when <paramref name="providerType"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="System.ArgumentException">
    ///   Thrown when no provider of that type was registered.
    /// </exception>
    MetadataProviderInfo GetProviderInfo(Type providerType);

    /// <summary>
    ///   Every registered provider.
    /// </summary>
    /// <remarks>
    ///   Each flag is a requirement, so both set returns only the providers
    ///   turned on for both halves, and neither set returns everything
    ///   registered whether it answers or not.
    /// </remarks>
    /// <param name="enabledForSeries">
    ///   Limit to providers turned on for a series, a season or an episode.
    /// </param>
    /// <param name="enabledForMovies">
    ///   Limit to providers turned on for a movie.
    /// </param>
    /// <returns>One <see cref="MetadataProviderInfo"/> per provider.</returns>
    IEnumerable<MetadataProviderInfo> GetAvailableProviders(bool enabledForSeries = false, bool enabledForMovies = false);

    /// <summary>
    ///   Gets the icon of a source: its series provider's, else its movie
    ///   provider's, in registration order, enabled or not. The core gives
    ///   AniDB's.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The icon, or <c>null</c> when the source has none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    PackageImageInfo? GetSourceIcon(MetadataSource source);

    /// <summary>
    ///   A flat list of the sources switched on.
    /// </summary>
    IReadOnlySet<MetadataSource> EnabledProviders { get; }

    /// <summary>
    ///   The providers allowed to answer for an entity type.
    /// </summary>
    /// <remarks>
    ///   Only enabled providers are returned.
    /// </remarks>
    /// <param name="entityType">The entity type wanted.</param>
    /// <param name="source">Limit to one source, or any when omitted.</param>
    /// <returns>One <see cref="MetadataProviderInfo"/> per provider.</returns>
    IEnumerable<MetadataProviderInfo> GetAvailableProviders(MetadataEntityType entityType, MetadataSource? source = null);

    /// <summary>
    ///   The providers a plugin registered.
    /// </summary>
    /// <param name="plugin">The plugin.</param>
    /// <returns>One <see cref="MetadataProviderInfo"/> per provider.</returns>
    IReadOnlyList<MetadataProviderInfo> GetProviderInfo(IPlugin plugin);

    /// <summary>
    ///   Whether a provider is currently allowed to answer.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>
    ///   <c>true</c> if it is on, otherwise <c>false</c>,
    ///   including when it was never registered.
    /// </returns>
    bool IsProviderEnabled(IMetadataProvider provider);

    /// <summary>
    ///   Whether a provider is currently allowed to answer, by its stable ID.
    /// </summary>
    /// <param name="providerID">The provider's ID.</param>
    /// <returns>
    ///   <c>true</c> if it is on, otherwise <c>false</c>,
    ///   including when nothing goes by that ID.
    /// </returns>
    bool IsProviderEnabled(Guid providerID);

    /// <summary>
    ///   Turns a provider on or off and saves the decision.
    /// </summary>
    /// <remarks>
    ///   A provider starts on for every entity type no earlier provider on its
    ///   source took, and stands by for the rest. On makes it answer for every
    ///   type it can; off stops it answering or standing by for any. The change
    ///   takes effect at once for what is fetched or refreshed next; a disabled
    ///   provider's stored data still reads.
    /// </remarks>
    /// <param name="provider">The provider.</param>
    /// <param name="enabled">Whether it should answer.</param>
    /// <exception cref="System.ArgumentNullException">
    ///   Thrown when <paramref name="provider"/> is <c>null</c>.
    /// </exception>
    void SetProviderEnabled(IMetadataProvider provider, bool enabled);

    /// <summary>
    ///   Turns a provider on or off by its stable ID, and saves the decision.
    /// </summary>
    /// <remarks>
    ///   Does nothing when nothing goes by that ID.
    /// </remarks>
    /// <param name="providerID">The provider's ID.</param>
    /// <param name="enabled">Whether it should answer.</param>
    void SetProviderEnabled(Guid providerID, bool enabled);

    /// <summary>
    ///   Turns a provider on for exactly these entity types on its source, and
    ///   saves the decision.
    /// </summary>
    /// <remarks>
    ///   An entity type the provider cannot answer for is dropped; an empty
    ///   set turns it off. One provider answers for a source and entity type
    ///   at a time, so turning this one on for a type moves it to the front of
    ///   the type's order, ahead of whichever provider had it. A type left out
    ///   is turned off for it, standing by included. Does nothing when nothing
    ///   goes by that ID.
    /// </remarks>
    /// <param name="providerID">The provider's ID.</param>
    /// <param name="enabled">The entity types to answer for.</param>
    /// <exception cref="ArgumentNullException"><paramref name="enabled"/> is <c>null</c>.</exception>
    void SetProviderEnabled(Guid providerID, IReadOnlySet<MetadataEntityType> enabled);

    /// <summary>
    ///   The providers claiming an entity type on a source, in the order they
    ///   are tried.
    /// </summary>
    /// <remarks>
    ///   The first enabled one answers, and only its
    ///   <see cref="MetadataProviderInfo.EnabledEntityTypes"/> lists the type.
    ///   The rest stand by and take over, in order, when it is turned off or
    ///   removed. A provider paused or not configured is not skipped.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <param name="entityType">The entity type.</param>
    /// <returns>Every registered provider claiming it, in order; none when nothing does.</returns>
    /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
    IReadOnlyList<MetadataProviderAssignment> GetProviderOrder(MetadataSource source, MetadataEntityType entityType);

    /// <summary>
    ///   Sets the order of the providers claiming entity types on a source,
    ///   and which of them are enabled, and saves the decision.
    /// </summary>
    /// <remarks>
    ///   The providers given come first, in the order given; those left out
    ///   keep their place and switch after them. Nothing is changed unless
    ///   every type and provider given is accepted.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <param name="orders">The order for each entity type to change.</param>
    /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   A provider is given twice for a type, or does not claim the type on
    ///   the source.
    /// </exception>
    void SetProviderOrder(MetadataSource source, IReadOnlyDictionary<MetadataEntityType, IReadOnlyList<MetadataProviderAssignment>> orders);

    /// <summary>
    ///   Sets which provider auto-links a source, and saves the decision.
    /// </summary>
    /// <remarks>
    ///   The first provider able to auto-link that registers for a source
    ///   takes it once, so this is how an admin hands it to another.
    /// </remarks>
    /// <param name="source">
    ///   The source to decide for. Only one provider auto-links a source, so
    ///   naming one takes it off whoever had it.
    /// </param>
    /// <param name="providerID">
    ///   The provider to use, both when a person asks and when it links on its
    ///   own, or <c>null</c> for nobody. It must implement
    ///   <see cref="Providers.IMetadataAutoLinkingProvider"/> and claim the
    ///   source. It only hands back what it finds; the core links it.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   Thrown when the provider is not an auto-linker, or does not claim the
    ///   source.
    /// </exception>
    void SetProviderAutoLinker(MetadataSource source, Guid? providerID);

    /// <summary>
    ///   Sets whether a source auto-links on its own, and saves the decision.
    /// </summary>
    /// <param name="source">
    ///   The source to decide for. Its auto-linker is set with
    ///   <see cref="SetProviderAutoLinker"/>, and this changes nothing while
    ///   it has none.
    /// </param>
    /// <param name="autoLink">
    ///   <c>true</c> to link when an anime is new and in the library
    ///   sweep; <c>false</c> to link only when a person asks.
    /// </param>
    void SetProviderAutoLink(MetadataSource source, bool autoLink);

    /// <summary>
    ///   Sets whether a source may auto-link restricted entries, and saves the
    ///   decision.
    /// </summary>
    /// <param name="source">
    ///   The source to decide for. Like <see cref="SetProviderAutoLink"/>,
    ///   this changes nothing while the source has no auto-linker.
    /// </param>
    /// <param name="autoLinkRestricted">
    ///   <c>true</c> to let its auto-linker link restricted entries
    ///   too; <c>false</c> to leave them to be linked by hand.
    /// </param>
    void SetProviderAutoLinkRestricted(MetadataSource source, bool autoLinkRestricted);

    /// <summary>
    ///   The sources the core resolves itself, which a plugin provider may not
    ///   claim.
    /// </summary>
    /// <remarks>
    ///   A provider declaring one of these as its
    ///   <see cref="IMetadataProvider.Source"/> is refused at registration.
    ///   The list may grow in later versions.
    /// </remarks>
    IReadOnlySet<MetadataSource> ReservedSources { get; }

    #endregion
}
