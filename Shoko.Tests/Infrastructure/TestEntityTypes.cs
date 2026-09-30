using Shoko.Abstractions.Metadata;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// Plugin entity types the tests use, registered once for the whole test run.
/// </summary>
/// <remarks>
/// The core only registers its own entity types, so anything standing in for
/// a plugin's kind is registered here, the way a plugin would register its
/// own.
/// </remarks>
public static class TestEntityTypes
{
    #region Entity Types

    static TestEntityTypes()
    {
        Library = MetadataEntityType.Register("Library", "library");
    }

    /// <summary>
    /// A media server library, as a plugin would register it.
    /// </summary>
    public static MetadataEntityType Library { get; }

    #endregion
}
