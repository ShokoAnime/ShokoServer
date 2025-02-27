
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Plugin.Abstractions.Hashing;

/// <summary>
///   
/// </summary>
public interface IHashProvider
{
    /// <summary>
    ///   Friendly name of the release information provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   Version of the release information provider.
    /// </summary>
    Version Version { get; }

    /// <summary>
    ///   Gets all available hash types for the provider.
    /// </summary>
    IReadOnlySet<string> AvailableHashTypes { get; }

    /// <summary>
    ///   Gets the default enabled hash types for the provider.
    /// </summary>
    IReadOnlySet<string> DefaultEnabledHashTypes { get; }

    /// <summary>
    ///   Gets all enabled hash types for a video file. The output is filtered
    ///   to only include enabled hash types, so providing other hash types will
    ///   have no effect and is generally not recommended.
    /// </summary>
    /// <param name="request">
    ///   A request for a video file to be hashed.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    ///   A list of hashes for the video file. The list is filtered to only
    ///   include enabled hash types inside the service.
    /// </returns>
    Task<IReadOnlyCollection<HashDigest>> GetHashesForVideo(HashingRequest request, CancellationToken cancellationToken = default);
}
