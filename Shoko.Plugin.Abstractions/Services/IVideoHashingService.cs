using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Hashing;

namespace Shoko.Plugin.Abstractions.Services;

/// <summary>
///   Service responsible for hashing video files.
/// </summary>
public interface IVideoHashingService
{
    /// <summary>
    ///   Event raised when a video file has been hashed. It is now ready to be
    ///   matched, and has been properly added to the database.
    /// </summary>
    event EventHandler<FileEventArgs> FileHashed;

    /// <summary>
    ///   Event raised when the hash providers are updated.
    /// </summary>
    event EventHandler? ProvidersUpdated;

    /// <summary>
    ///   Gets or sets a value indicating whether to use parallel mode.
    /// </summary>
    bool ParallelMode { get; set; }

    /// <summary>
    ///   Gets the set of all available hash types across all providers.
    /// </summary>
    IReadOnlySet<string> AllAvailableHashTypes { get; }

    /// <summary>
    ///   Gets the set of all enabled hash types across all providers.
    /// </summary>
    IReadOnlySet<string> AllEnabledHashTypes { get; }

    /// <summary>
    ///   Adds the hash providers.
    /// </summary>
    /// <remarks>
    ///   This should be called once per instance of the service. Calling it
    ///   multiple times will have no effect.
    /// </remarks>
    /// <param name="providers">
    ///   The hash providers.
    /// </param>
    void AddProviders(IEnumerable<IHashProvider> providers);

    /// <summary>
    ///   Gets the set of providers that are available, optionally filtered by
    ///   enabled state.
    /// </summary>
    /// <param name="onlyEnabled">
    ///   If true, only returns enabled providers.
    /// </param>
    /// <returns>
    ///   The available providers.
    /// </returns>
    IEnumerable<HashProviderInfo> GetAvailableProviders(bool onlyEnabled = false);

    /// <summary>
    ///   Updates the providers. This can be used to update the enabled state or
    ///   add/remove providers.
    /// </summary>
    /// <param name="providers">
    ///   The providers to update.
    /// </param>
    void UpdateProviders(params HashProviderInfo[] providers);

    /// <summary>
    ///   Gets the provider info for a given provider ID.
    /// </summary>
    /// <param name="providerID">
    ///   The provider ID.
    /// </param>
    /// <returns>
    ///   The provider info, or <c>null</c> if not found.
    /// </returns>
    HashProviderInfo? GetProviderInfo(Guid providerID);

    /// <summary>
    ///   Gets the provider info for a given provider.
    /// </summary>
    /// <param name="provider">
    ///   The provider.
    /// </param>
    /// <returns>
    ///   The provider info.
    /// </returns>
    HashProviderInfo GetProviderInfo(IHashProvider provider);

    /// <summary>
    ///   Gets the provider info for a given provider type.
    /// </summary>
    /// <typeparam name="TProvider">
    ///   The provider type.
    /// </typeparam>
    /// <returns>
    ///   The provider info.
    /// </returns>
    HashProviderInfo GetProviderInfo<TProvider>() where TProvider : class, IHashProvider;

    /// <summary>
    ///   Gets the hashes for a given video file.
    /// </summary>
    /// <remarks>
    ///   If the file is already in the database, it will be returned from the
    ///   database. If it is not, it will be hashed and added to the database.
    /// </remarks>
    /// <param name="fileInfo">
    ///   The file to hash.
    /// </param>
    /// <param name="existingHashes">
    ///   The existing hashes for the file, if any.
    /// </param>
    /// <param name="cancellationToken">
    ///   The cancellation token.
    /// </param>
    /// <returns>
    ///   The hashes for the file.
    /// </returns>
    Task<IReadOnlyList<IHashDigest>> GetHashesForFile(FileInfo fileInfo, IReadOnlyList<IHashDigest>? existingHashes = null, CancellationToken cancellationToken = default);
}

