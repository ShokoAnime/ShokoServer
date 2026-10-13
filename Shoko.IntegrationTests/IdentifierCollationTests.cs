using System;
using System.Data;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Databases;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.Metadata;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Writes IDs an insensitive collation takes for one, which every backend
/// has to keep apart as the server does in memory.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class IdentifierCollationTests(DatabaseMigrationFixture fixture)
{
    private const int AnimeID = 990_101;

    private static readonly MetadataSource _plugin = TestSources.Plugin;

    /// <summary>
    /// IDs differing only in case, in an accent, in the kana script and in width.
    /// </summary>
    private static readonly string[] _ids = ["idc/shonen", "idc/Shonen", "idc/shōnen", "idc/しょうねん", "idc/ショウネン", "idc/ｓｈｏｎｅｎ"];

    [Fact]
    public void IDsACollationTakesForOneAreSeparateRows()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var source = MetadataNumberRegistry.GetNumber(_plugin);
        var tags = fixture.Services.GetRequiredService<IMetadataTagStore>();
        var links = fixture.Services.GetRequiredService<CrossRef_AniDB_Metadata_SeriesRepository>();
        var saved = _ids
            .Select((id, index) => new CrossRef_AniDB_Metadata_Series { Source = _plugin, AnidbAnimeID = AnimeID, ProviderID = id, Ordering = index })
            .ToList();
        using var connection = fixture.OpenConnection();
        try
        {
            tags.SaveTags(_ids.Select(id => new MetadataTagData { ID = new(_plugin, MetadataEntityType.Tag, id), Name = id }));
            foreach (var link in saved)
                links.Save(link);

            // Each ID finds its own row and no other, in a unique index and in a lookup alike.
            var tagQuery = $"SELECT COUNT(*) FROM Metadata_Tag WHERE Source = {source} AND ProviderID = @id";
            var linkQuery = $"SELECT COUNT(*) FROM CrossRef_AniDB_Metadata_Series WHERE Source = {source} AND AnidbAnimeID = {AnimeID} AND ProviderID = @id";
            foreach (var id in _ids)
            {
                Assert.Equal(1, Count(connection, tagQuery, id));
                Assert.Equal(1, Count(connection, linkQuery, id));
            }
        }
        finally
        {
            foreach (var link in saved.Where(link => link.CrossRef_AniDB_Metadata_SeriesID is not 0))
                links.Delete(link);
            Sql.Execute(connection, $"DELETE FROM Metadata_Tag WHERE Source = {source} AND ProviderID LIKE 'idc/%'");
            fixture.Services.GetRequiredService<Metadata_TagRepository>().Populate(displayName: false, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Counts the rows a query with one ID parameter finds.
    /// </summary>
    /// <param name="connection">The open connection.</param>
    /// <param name="sql">The counting query, naming the ID <c>@id</c>.</param>
    /// <param name="id">The ID.</param>
    /// <returns>The number of rows.</returns>
    private static int Count(IDbConnection connection, string sql, string id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = id;
        command.Parameters.Add(parameter);
        return Convert.ToInt32(command.ExecuteScalar());
    }
}
