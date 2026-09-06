using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Shares one server bootstrap across every test class in the collection.
/// </summary>
/// <remarks>
/// <c>ISystemService.StaticServices</c> is write-once per process, so a second
/// <see cref="DatabaseMigrationFixture"/> throws. A class fixture is one instance per class; this is
/// one for the run.
/// </remarks>
[CollectionDefinition(Name)]
public class DatabaseCollection : ICollectionFixture<DatabaseMigrationFixture>
{
    public const string Name = "Database";
}
