using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Plugin;

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
    ///   The info, or <see langword="null"/> if nothing goes by that ID.
    /// </returns>
    MetadataProviderInfo? GetProviderInfo(Guid providerID);

    /// <summary>
    ///   Looks up a provider's info from the provider itself.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>The info.</returns>
    /// <exception cref="System.ArgumentNullException">
    ///   Thrown when <paramref name="provider"/> is <see langword="null"/>.
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
    ///   Thrown when <paramref name="providerType"/> is <see langword="null"/>.
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
    ///   <see langword="true"/> if it is on, otherwise <see langword="false"/>,
    ///   including when it was never registered.
    /// </returns>
    bool IsProviderEnabled(IMetadataProvider provider);

    /// <summary>
    ///   Whether a provider is currently allowed to answer, by its stable ID.
    /// </summary>
    /// <param name="providerID">The provider's ID.</param>
    /// <returns>
    ///   <see langword="true"/> if it is on, otherwise <see langword="false"/>,
    ///   including when nothing goes by that ID.
    /// </returns>
    bool IsProviderEnabled(Guid providerID);

    /// <summary>
    ///   Turns a provider on or off and saves the decision.
    /// </summary>
    /// <remarks>
    ///   A provider starts on for every entity type no earlier provider on its
    ///   source took. The change takes effect at once for what is fetched or
    ///   refreshed next; a disabled provider's stored data still reads.
    /// </remarks>
    /// <param name="provider">The provider.</param>
    /// <param name="enabled">Whether it should answer.</param>
    /// <exception cref="System.ArgumentNullException">
    ///   Thrown when <paramref name="provider"/> is <see langword="null"/>.
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
    ///   An entity type the provider cannot answer for is
    ///   dropped; an empty set turns it off. One provider answers for a
    ///   source and entity type at a time, so turning this one on for a type
    ///   takes the type off whichever provider had it. Does nothing when
    ///   nothing goes by that ID.
    /// </remarks>
    /// <param name="providerID">The provider's ID.</param>
    /// <param name="enabled">The entity types to answer for.</param>
    /// <exception cref="ArgumentNullException"><paramref name="enabled"/> is <see langword="null"/>.</exception>
    void SetProviderEnabled(Guid providerID, IReadOnlySet<MetadataEntityType> enabled);

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
    ///   own, or <see langword="null"/> for nobody. It must implement
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
    ///   <see langword="true"/> to link when an anime is new and in the library
    ///   sweep; <see langword="false"/> to link only when a person asks.
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
    ///   <see langword="true"/> to let its auto-linker link restricted entries
    ///   too; <see langword="false"/> to leave them to be linked by hand.
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
