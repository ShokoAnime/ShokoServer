using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video.Services;

namespace Shoko.Abstractions.Video;

/// <summary>
///   Reads the direct links between videos and entries for the default
///   members of the entry and video contracts, which have no other way to
///   reach the release service.
/// </summary>
internal static class MetadataVideoLinks
{
    /// <summary>
    ///   The direct links to an entry.
    /// </summary>
    /// <param name="id">The entry.</param>
    /// <returns>The links, or none before the server has started.</returns>
    internal static IReadOnlyList<IMetadataVideoCrossReference> To(MetadataGuid id)
        => Service()?.GetMetadataVideoCrossReferences(id) ?? [];

    /// <summary>
    ///   The direct links of a video.
    /// </summary>
    /// <param name="video">The video.</param>
    /// <returns>The links, or none before the server has started.</returns>
    internal static IReadOnlyList<IMetadataVideoCrossReference> Of(IVideo video)
        => Service()?.GetMetadataVideoCrossReferences(video) ?? [];

    private static IVideoReleaseService? Service()
    {
#pragma warning disable CS0618
        return ISystemService.HasStaticServices ? ISystemService.StaticServices.GetService<IVideoReleaseService>() : null;
#pragma warning restore CS0618
    }
}
