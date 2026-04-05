using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Verifies that all database migrations run without error against the backend
/// configured via environment variables (see <see cref="DatabaseMigrationFixture"/>).
/// </summary>
[Collection("Database")]
public class DatabaseMigrationTests : IClassFixture<DatabaseMigrationFixture>
{
    private readonly DatabaseMigrationFixture _fixture;

    public DatabaseMigrationTests(DatabaseMigrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void MigrationsCompleteSuccessfully()
    {
        Assert.True(_fixture.Success, _fixture.FailureMessage ?? "Database initialization failed");
    }
}
