
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Config;

namespace Shoko.Abstractions.Hashing;

/// <summary>
///   Base interface for hash providers to implement.
/// </summary>
public interface IHashProvider
{
    /// <summary>
    ///   Friendly name of the hash provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   Optional. Description of the hash provider.
    /// </summary>
    string? Description { get => null; }

    /// <summary>
    ///   Version of the hash provider.
    /// </summary>
    Version Version { get => GetType().Assembly.GetName().Version ?? new Version(0, 0, 0, 0); }

    /// <summary>
    ///   Gets all available hash types for the provider.
    /// </summary>
    IReadOnlySet<string> AvailableHashTypes { get; }

    /// <summary>
    ///   Gets all enabled hash types for a video file. The output is filtered
    ///   to only include enabled hash types, so computing other hash types will
    ///   have no effect and is generally not recommended.
    /// </summary>
    /// <param name="request">
    ///   A request for a video file to be hashed.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    ///   A collection of hashes for the video file. The collection will be
    ///   filtered to only include enabled hash types inside the service, so
    ///   to reduce wasted computation it's recommended to compute only enabled
    ///   hash types in the request.
    /// </returns>
    Task<IReadOnlyCollection<HashDigest>> GetHashesForVideo(HashingRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
///   Indicates that the hash provider supports configuration, and which
///   configuration type to display in the UI.
/// </summary>
/// <typeparam name="TConfiguration">
///   The hash provider configuration type.
/// </typeparam>
public interface IHashProvider<TConfiguration> : IHashProvider where TConfiguration : IHashProviderConfiguration { }
