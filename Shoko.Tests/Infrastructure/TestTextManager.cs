using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Services;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// Builds a text manager over a test's text store, for the models to read their titles and
/// overviews through once <see cref="RepoFactoryScope.Set(MetadataTextManager)"/> puts it in place.
/// </summary>
public static class TestTextManager
{
    /// <summary>
    /// Builds the manager, with the stub settings installed for its language walks.
    /// </summary>
    /// <param name="store">The text store the test's stores write through.</param>
    /// <param name="service">Finds entries by ID; one that finds nothing when left out.</param>
    /// <returns>The manager.</returns>
    public static MetadataTextManager Build(MetadataTextStore store, IMetadataService? service = null)
    {
        StubSettingsProvider.Install();
        return new(service ?? Mock.Of<IMetadataService>(), store, NullLogger<MetadataTextManager>.Instance);
    }
}
