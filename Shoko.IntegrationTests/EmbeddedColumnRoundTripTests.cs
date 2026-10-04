using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Extensions;
using Shoko.Server.Models.Release;
using Shoko.Server.Repositories.Cached;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
///   Saves releases whose embedded columns the cache holds as lists, reloads
///   them, saves them again and compares the stored columns, which must not
///   change.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class EmbeddedColumnRoundTripTests(DatabaseMigrationFixture fixture)
{
    private const string ED2K = "E3BEDDED0000000000000000000000AA";

    // Spaced out, so the cache cannot write it back from its list.
    private const string LooseCrossReferences = """[ {"ProviderIDs": {"AniDB_Episode": "990501"}, "PercentageStart": 0, "PercentageEnd": 100} ]""";

    private const string CrossReferences = """[{"ProviderIDs":{"AniDB_Episode":"990502","AniDB_Anime":"990500"},"PercentageStart":0,"PercentageEnd":100}]""";

    private const string Hashes = """[{"Type":"ED2K","Value":"E3BEDDED0000000000000000000000AA","Metadata":null},{"Type":"CRC32","Value":"C859A673","Metadata":null}]""";

    [Fact]
    public void AReleasesEmbeddedColumnsSurviveALoadAndASave()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var releases = fixture.Services.GetRequiredService<StoredReleaseInfoRepository>();
        var loose = Release(1, LooseCrossReferences, " " + Hashes);
        var exact = Release(2, CrossReferences, Hashes);
        releases.Save([loose, exact]);
        try
        {
            using var connection = fixture.OpenConnection();
            var written = ReadReleaseColumns(connection);

            releases.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);
            var reloaded = releases.GetByEd2k(ED2K).OrderBy(release => release.FileSize).ToList();
            Assert.Equal([990501, 990502], reloaded.SelectMany(release => release.CrossReferences).Select(xref => xref.AnidbEpisodeID));
            Assert.Equal(["ED2K", "CRC32"], reloaded[1].Hashes!.Select(hash => hash.Type));
            reloaded.ForEach(release => release.PreventRescan = true);
            releases.Save(reloaded);

            Assert.Equal(
                [
                    $"1001|{LooseCrossReferences}| {Hashes}",
                    $"1002|{CrossReferences}|{Hashes}",
                ],
                written
            );
            Assert.Equal(written, ReadReleaseColumns(connection));
        }
        finally
        {
            releases.Delete(releases.GetByEd2k(ED2K).ToList());
        }
    }

    private static StoredReleaseInfo Release(int index, string crossReferences, string hashes)
        => new()
        {
            ED2K = ED2K,
            FileSize = 1_000 + index,
            ProviderName = "AniDB",
            EmbeddedCrossReferences = crossReferences,
            EmbeddedHashes = hashes,
            CreatedAt = DateTime.Now,
            LastUpdatedAt = DateTime.Now,
        };

    private static List<string> ReadReleaseColumns(IDbConnection connection)
        => Sql.Read(connection, $"SELECT FileSize, CrossReferences, Hashes FROM StoredReleaseInfo WHERE ED2K = '{ED2K}' ORDER BY FileSize");
}
