using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.Models.Release;
using Shoko.Server.Repositories.Cached;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Runs the schema step that turns AniDB's corrupted flag into the deprecated
/// one for releases whose provider chain has AniDB in the middle, which the
/// released step for the other chains missed.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ReleaseDeprecationStepTests(DatabaseMigrationFixture fixture)
{
    #region Helpers

    private const string ED2KPrefix = "5E1EA5EDE9EC47ED";

    /// <summary>
    /// The backend's step for chains with AniDB in the middle.
    /// </summary>
    /// <returns>The step's SQL.</returns>
    private string MiddleOfChainStep()
        => SchemaSteps.GetSql(fixture, 112);

    private static StoredReleaseInfo Release(int index, string providerName, bool isCorrupted)
        => new()
        {
            ED2K = $"{ED2KPrefix}{index:D16}",
            FileSize = 1_000 + index,
            ProviderName = providerName,
            IsCorrupted = isCorrupted,
            CreatedAt = DateTime.Now,
            LastUpdatedAt = DateTime.Now,
        };

    private static Dictionary<string, (bool IsDeprecated, bool IsCorrupted)> ReadFlags(IDbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT ED2K, IsDeprecated, IsCorrupted FROM StoredReleaseInfo WHERE ED2K LIKE '{ED2KPrefix}%'";
        using var reader = command.ExecuteReader();
        var flags = new Dictionary<string, (bool, bool)>(StringComparer.Ordinal);
        while (reader.Read())
            flags[reader.GetString(0)] = (Convert.ToInt32(reader.GetValue(1)) != 0, Convert.ToInt32(reader.GetValue(2)) != 0);
        return flags;
    }

    #endregion

    #region Tests

    [Fact]
    public void ACorruptedReleaseWithAniDBInTheMiddleOfItsChain_IsMarkedDeprecatedInstead()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var repository = fixture.Services.GetRequiredService<StoredReleaseInfoRepository>();
        var middle = Release(1, "Plugin+AniDB+Other", true);
        var intact = Release(2, "Plugin+AniDB+Other", false);
        var lookalike = Release(3, "Plugin+AniDBx+Other", true);
        var plugin = Release(4, "Plugin", true);
        StoredReleaseInfo[] releases = [middle, intact, lookalike, plugin];
        repository.Save(releases);

        try
        {
            using var connection = fixture.OpenConnection();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = MiddleOfChainStep();
                command.ExecuteNonQuery();
            }

            var flags = ReadFlags(connection);
            Assert.Equal((true, false), flags[middle.ED2K]);
            Assert.Equal((false, false), flags[intact.ED2K]);
            Assert.Equal((false, true), flags[lookalike.ED2K]);
            Assert.Equal((false, true), flags[plugin.ED2K]);
        }
        finally
        {
            repository.Delete(releases);
        }
    }

    #endregion
}
