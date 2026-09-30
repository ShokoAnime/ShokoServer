using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Storage;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// A cross-reference store that holds no links, for tests of a store whose
/// removals look for links but are not about them.
/// </summary>
public static class NoLinks
{
    /// <summary>
    /// Builds the store.
    /// </summary>
    /// <returns>A mock whose lookups find no links; its writes do nothing.</returns>
    public static Mock<IMetadataCrossReferenceStore> Build()
    {
        var links = new Mock<IMetadataCrossReferenceStore>();
        links.Setup(store => store.GetLinksTo(It.IsAny<MetadataGuid>())).Returns([]);
        return links;
    }
}
