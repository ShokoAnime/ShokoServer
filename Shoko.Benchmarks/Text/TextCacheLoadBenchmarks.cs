using BenchmarkDotNet.Attributes;
using Microsoft.Data.Sqlite;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.Cached.Metadata.Text;

namespace Benchmarks.Text;

/// <summary>
///   What loading text into memory costs at startup: the text cache reading the AniDB titles, and the series warm-up.
/// </summary>
/// <remarks>
///   Each load reads a private in-memory SQLite database holding the fixture's stored AniDB anime or episode titles,
///   so the plain query and the building of the cache are both included. Divide the fixture's row counts
///   (<c>--fixture</c>) by the mean to get rows per second.
/// </remarks>
[BenchmarkCategory("Text")]
[MemoryDiagnoser]
public class TextCacheLoadBenchmarks
{
    private TextFixture _fixture = null!;

    private SqliteConnection _animeTitles = null!;

    private SqliteConnection _episodeTitles = null!;

    [GlobalSetup]
    public void Setup()
    {
        _fixture = TextFixture.Instance;
        _animeTitles = Database(MetadataEntityType.Series);
        _episodeTitles = Database(MetadataEntityType.Episode);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _animeTitles.Dispose();
        _episodeTitles.Dispose();
    }

    [Benchmark]
    public int AnidbAnimeTitles()
        => Load(_animeTitles);

    [Benchmark]
    public int AnidbEpisodeTitles()
        => Load(_episodeTitles);

    /// <summary>
    ///   The series warm-up <c>AnimeSeriesRepository.RegenerateDb</c> runs at startup.
    /// </summary>
    [Benchmark]
    public int WarmSeriesText()
    {
        var count = 0;
        foreach (var series in _fixture.Series)
        {
            TextAccess.Forget(((IMetadata)series).ID);
            count += series.PreferredTitle is null ? 0 : 1;
            count += series.PreferredOverview is null ? 0 : 1;
            count += series.Titles.Count;
        }

        return count;
    }

    private static int Load(SqliteConnection connection)
    {
        var cache = new TextCache();
        cache.Load(connection);
        return cache.EntryCount;
    }

    /// <summary>
    ///   An in-memory database with the two text tables, holding the fixture's stored AniDB titles of one kind of entry.
    /// </summary>
    /// <param name="entityType">The kind of entry whose titles to hold.</param>
    /// <returns>The open connection.</returns>
    private SqliteConnection Database(MetadataEntityType entityType)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        Execute(connection, "CREATE TABLE Metadata_Title (Metadata_TitleID INTEGER PRIMARY KEY AUTOINCREMENT, EntitySource INTEGER NOT NULL, EntityType INTEGER NOT NULL, EntityID NVARCHAR(128) NOT NULL, Source INTEGER NOT NULL, Language NVARCHAR(32) NOT NULL, LanguageCode NVARCHAR(32) NOT NULL, CountryCode NVARCHAR(32) NULL, ScriptCode NVARCHAR(8) NULL, TitleType INTEGER NOT NULL, Value TEXT NOT NULL, IsEnabled INTEGER NOT NULL, Preference INTEGER NOT NULL, Ordering INTEGER NOT NULL, ReferenceID INTEGER NULL);");
        Execute(connection, "CREATE TABLE Metadata_Overview (Metadata_OverviewID INTEGER PRIMARY KEY AUTOINCREMENT, EntitySource INTEGER NOT NULL, EntityType INTEGER NOT NULL, EntityID NVARCHAR(128) NOT NULL, Source INTEGER NOT NULL, Language NVARCHAR(32) NOT NULL, LanguageCode NVARCHAR(32) NOT NULL, CountryCode NVARCHAR(32) NULL, ScriptCode NVARCHAR(8) NULL, Value TEXT NOT NULL, IsEnabled INTEGER NOT NULL, Preference INTEGER NOT NULL, Ordering INTEGER NOT NULL, ReferenceID INTEGER NULL);");

        using var transaction = connection.BeginTransaction();
        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = "INSERT INTO Metadata_Title (EntitySource, EntityType, EntityID, Source, Language, LanguageCode, CountryCode, ScriptCode, TitleType, Value, IsEnabled, Preference, Ordering, ReferenceID) " +
            "VALUES (@s, @t, @e, @s, @l, @c, NULL, NULL, @y, @v, 1, 0, @o, NULL)";
        foreach (var name in (string[])["@s", "@t", "@e", "@l", "@c", "@y", "@v", "@o"])
            insert.Parameters.Add(new SqliteParameter(name, null));

        var cache = _fixture.TextStore.Cache;
        foreach (var (entity, rows) in cache.Enumerate())
        {
            if (entity.Source != MetadataSource.AniDB || entity.EntityType != entityType)
                continue;

            foreach (var row in rows)
            {
                var stored = cache.ToRow(entity, row);
                insert.Parameters["@s"].Value = (int)MetadataNumberRegistry.GetNumber(MetadataSource.AniDB);
                insert.Parameters["@t"].Value = (int)MetadataNumberRegistry.GetNumber(entityType);
                insert.Parameters["@e"].Value = entity.ID;
                insert.Parameters["@l"].Value = stored.Language.GetString();
                insert.Parameters["@c"].Value = stored.LanguageCode;
                insert.Parameters["@y"].Value = (int)stored.TitleType;
                insert.Parameters["@v"].Value = stored.Value;
                insert.Parameters["@o"].Value = stored.Ordering;
                insert.ExecuteNonQuery();
            }
        }

        transaction.Commit();
        return connection;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
