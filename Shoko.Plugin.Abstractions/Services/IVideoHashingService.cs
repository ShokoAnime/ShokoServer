using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Hashing;

namespace Shoko.Plugin.Abstractions.Services;

/// <summary>
///   Service responsible for hashing video files.
/// </summary>
/// <remarks>
///   The service can operate in sequential mode or parallel model. In
///   sequential mode it will run each enabled provider in order of priority,
///   while in parallel mode it will run all enabled providers in parallel. The
///   latter can increase performance if your system can handle it, but may also
///   cause slowdowns if it overloads your system.
/// </remarks>
public interface IVideoHashingService
{
    /// <summary>
    ///   Event raised when a video file has been hashed. It is now been
    ///   properly added to the database and is ready for use.
    /// </summary>
    event EventHandler<FileHashedEventArgs> FileHashed;

    /// <summary>
    ///   Event raised when the enabled hash providers are updated or parallel
    ///   mode is changed.
    /// </summary>
    event EventHandler? ProvidersUpdated;

    /// <summary>
    ///   Event raised when all hash providers are registered and the service is
    ///   ready for use.
    /// </summary>
    event EventHandler? Ready;

    /// <summary>
    ///   Gets or sets a value indicating whether to use parallel mode. Parallel
    ///   mode will allow all providers to hash the same file at the same time.
    ///   <br/>
    ///   This can increase performance if your system can handle it, but may
    ///   also cause slowdowns if it overloads your system.
    /// </summary>
    bool ParallelMode { get; set; }

    /// <summary>
    ///   Gets the read-only set of all available hash types across all providers.
    /// </summary>
    IReadOnlySet<string> AllAvailableHashTypes { get; }

    /// <summary>
    ///   Gets the read-only set of all enabled hash types across all providers.
    /// </summary>
    IReadOnlySet<string> AllEnabledHashTypes { get; }

    /// <summary>
    ///   Adds the needed parts for the service to function.
    /// </summary>
    /// <remarks>
    ///   This should be called once per instance of the service, and will be
    ///   called during start-up. Calling it multiple times will have no effect.
    /// </remarks>
    /// <param name="providers">
    ///   The hash providers.
    /// </param>
    void AddParts(IEnumerable<IHashProvider> providers);

    /// <summary>
    ///   Gets all providers that are available, optionally filtered by enabled
    ///   state.
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
    ///   Gets the <see cref="HashProviderInfo"/> for a given plugin.
    /// </summary>
    /// <param name="plugin">
    ///   The plugin.
    /// </param>
    /// <returns>
    ///   The provider info.
    /// </returns>
    IReadOnlyList<HashProviderInfo> GetProviderInfo(IPlugin plugin);

    /// <summary>
    ///   Gets the <see cref="HashProviderInfo"/> for a given provider ID.
    /// </summary>
    /// <param name="providerID">
    ///   The provider ID.
    /// </param>
    /// <returns>
    ///   The provider info, or <c>null</c> if not found.
    /// </returns>
    HashProviderInfo? GetProviderInfo(Guid providerID);

    /// <summary>
    ///   Gets the <see cref="HashProviderInfo"/> for a given provider.
    /// </summary>
    /// <param name="provider">
    ///   The provider.
    /// </param>
    /// <returns>
    ///   The provider info.
    /// </returns>
    HashProviderInfo GetProviderInfo(IHashProvider provider);

    /// <summary>
    ///   Gets the <see cref="HashProviderInfo"/> for a given provider type.
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
    /// <param name="path">
    ///   The path to the video file.
    /// </param>
    /// <param name="useExistingHashes">
    ///   If true, the existing hashes will be provided to the providers during the hashing process.
    /// </param>
    /// <param name="cancellationToken">
    ///   The cancellation token.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown if the file is not a video or video container.
    /// </exception>
    /// <returns>
    ///   The hashes for the file.
    /// </returns>
    Task<HashingResult> GetHashesForPath(string path, bool useExistingHashes = true, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Schedules the hashing of a video file in the queue.
    /// </summary>
    /// <remarks>
    ///   This will add the file to the queue for hashing. If the file is already
    ///   in the queue, it will be ignored.
    /// </remarks>
    /// <param name="path">
    ///   The path to the video file.
    /// </param>
    /// <param name="useExistingHashes">
    ///   If true, the existing hashes will be provided to the providers during
    ///   the hashing process.
    /// </param>
    /// <param name="prioritize">
    ///   If true, the hashing of the file will be prioritized in the queue.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown if the file is not a video or video container.
    /// </exception>
    /// <returns>
    ///   A task that represents the asynchronous operation of adding the file
    ///   to the queue.
    /// </returns>
    Task ScheduleGetHashesForPath(string path, bool useExistingHashes = true, bool prioritize = false);

    /// <summary>
    ///   Gets the hashes for a given video file.
    /// </summary>
    /// <param name="file"> The file to hash. </param>
    /// <param name="useExistingHashes">
    ///   If true, the existing hashes will be provided to the providers during
    ///   the hashing process.
    /// </param>
    /// <param name="cancellationToken">
    ///   The cancellation token.
    /// </param>
    /// <returns>
    ///   The hashes for the file.
    /// </returns>
    Task<HashingResult> GetHashesForFile(IVideoFile file, bool useExistingHashes = true, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Schedules the hashing of a video file in the queue.
    /// </summary>
    /// <remarks>
    ///   This will add the file to the queue for hashing. If the file is already
    ///   in the queue, it will be ignored.
    /// </remarks>
    /// <param name="file"> The file to hash. </param>
    /// <param name="useExistingHashes">
    ///   If true, the existing hashes will be provided to the providers during
    ///   the hashing process.
    /// </param>
    /// <param name="prioritize">
    ///   If true, the hashing of the file will be prioritized in the queue.
    /// </param>
    /// <returns>
    ///   A task that represents the asynchronous operation of adding the file
    ///   to the queue.
    /// </returns>
    Task ScheduleGetHashesForFile(IVideoFile file, bool useExistingHashes = true, bool prioritize = false);
}
