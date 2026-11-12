using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Models.Release;

/// <summary>
///   Resolves entries for the models built outside dependency injection, the
///   videos and their direct links.
/// </summary>
internal static class MetadataEntryResolver
{
    /// <summary>
    ///   Resolves an entry through the metadata service.
    /// </summary>
    /// <param name="id">The entry.</param>
    /// <returns>The entry, or <c>null</c> when nothing holds it or the server has not started.</returns>
    public static IMetadata? Resolve(MetadataGuid id)
    {
#pragma warning disable CS0618
        return ISystemService.HasStaticServices ? ISystemService.StaticServices.GetService<IMetadataService>()?.GetEntry(id) : null;
#pragma warning restore CS0618
    }
}
