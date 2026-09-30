using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Filtering.Expressions.Info;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Counts the series missing a link on a source in the migrated database, so
/// the query behind <c>/api/v3/Dashboard/MissingLinks</c> is checked on each
/// backend: the anime types, the veto column and both link tables.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MissingLinkCountTests(DatabaseMigrationFixture fixture)
{
    #region Seeding

    private const int FirstAnimeID = 991_001;

    private static readonly MetadataSource _plugin = TestSources.Plugin;

    private static readonly IReadOnlyCollection<AnimeType> _neverLinked = MissingSourceLinkExpression.AnimeTypes;

    /// <summary>
    /// One anime per rule, numbered from <see cref="FirstAnimeID"/>. Each
    /// entry is the anime's type (or <c>null</c> for a series whose anime is
    /// not stored), its veto column, and its links.
    /// </summary>
    private static readonly (AnimeType? Type, string? Veto, Func<int, CrossRef_AniDB_Metadata>[] Links)[] _anime =
    [
        // 1: nothing at all, so missing everywhere.
        (AnimeType.TV, null, []),
        // 2: linked on the plugin.
        (AnimeType.TV, null, [id => SeriesLink(_plugin, id, "show-2")]),
        // 3: linked to nothing on the plugin on purpose.
        (AnimeType.TV, null, [id => SeriesLink(_plugin, id, string.Empty)]),
        // 4: a film linked on the plugin at the movie level.
        (AnimeType.Movie, null, [id => MovieLink(_plugin, id, "film-4")]),
        // 5: a type no source links.
        (AnimeType.MusicVideo, null, []),
        // 6: the plugin is vetoed, written as the value.
        (AnimeType.TV, "[\"test-plugin\"]", []),
        // 7: TMDB is vetoed, written under its old name.
        (AnimeType.OVA, "[\"TMDB\"]", []),
        // 8: a series whose AniDB anime is not stored.
        (null, null, []),
        // 9: an unknown type.
        (AnimeType.Unknown, null, []),
        // 10: a veto of another source whose value starts the same.
        (AnimeType.Web, "[\"test-plugin-extra\"]", []),
        // 11: linked on TMDB only.
        (AnimeType.TV, null, [id => SeriesLink(MetadataSource.TMDB, id, "11")]),
    ];

    private static CrossRef_AniDB_Metadata_Series SeriesLink(MetadataSource source, int animeID, string providerID)
        => new() { Source = source, AnidbAnimeID = animeID, ProviderID = providerID };

    private static CrossRef_AniDB_Metadata_Movie MovieLink(MetadataSource source, int animeID, string providerID)
        => new() { Source = source, AnidbAnimeID = animeID, AnidbEpisodeID = animeID * 10, ProviderID = providerID };

    private static int AnimeID(int number)
        => FirstAnimeID + number - 1;

    /// <summary>
    /// Writes the rows straight into the database, past the repositories and
    /// their caches, since only the query is being checked.
    /// </summary>
    /// <returns>The rows to remove afterwards.</returns>
    private List<object> Seed()
    {
        var rows = new List<object>();
        var now = DateTime.Now;
        for (var index = 0; index < _anime.Length; index++)
        {
            var (type, veto, links) = _anime[index];
            var animeID = AnimeID(index + 1);
            // The obsolete column is still not nullable, and SQL Server keeps no date as early as the default.
#pragma warning disable CS0618
            if (type is { } animeType)
                rows.Add(new AniDB_Anime { AnimeID = animeID, AnimeType = animeType, MainTitle = $"Anime {animeID}", DateTimeUpdated = now, DateTimeDescUpdated = now });
#pragma warning restore CS0618
            rows.Add(new AnimeSeries { AniDB_ID = animeID, DisabledAutoMatchSources = veto, DateTimeCreated = now, DateTimeUpdated = now, UpdatedAt = now });
            rows.AddRange(links.Select(link => link(animeID)));
        }

        using var session = fixture.Services.GetRequiredService<DatabaseFactory>().SessionFactory.OpenStatelessSession();
        using var transaction = session.BeginTransaction();
        foreach (var row in rows)
            session.Insert(row);
        transaction.Commit();
        return rows;
    }

    private void Remove(List<object> rows)
    {
        using var session = fixture.Services.GetRequiredService<DatabaseFactory>().SessionFactory.OpenStatelessSession();
        using var transaction = session.BeginTransaction();
        foreach (var row in Enumerable.Reverse(rows))
            session.Delete(row);
        transaction.Commit();
    }

    #endregion

    #region Tests

    [Fact]
    public void TheSeriesMissingALinkAreFoundInTheDatabase()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var repository = fixture.Services.GetRequiredService<AnimeSeriesRepository>();
        var pluginBefore = repository.CountMissingLinks(_plugin, _neverLinked);
        var tmdbBefore = repository.CountMissingLinks(MetadataSource.TMDB, _neverLinked);
        var rows = Seed();
        try
        {
            var seeded = Enumerable.Range(1, _anime.Length).Select(AnimeID).ToHashSet();

            var plugin = repository.GetAnimeIDsMissingLinks(_plugin, _neverLinked).Where(seeded.Contains).Order().ToArray();
            Assert.Equal([AnimeID(1), AnimeID(7), AnimeID(10), AnimeID(11)], plugin);
            Assert.Equal(pluginBefore + plugin.Length, repository.CountMissingLinks(_plugin, _neverLinked));

            var tmdb = repository.GetAnimeIDsMissingLinks(MetadataSource.TMDB, _neverLinked).Where(seeded.Contains).Order().ToArray();
            Assert.Equal([AnimeID(1), AnimeID(2), AnimeID(3), AnimeID(4), AnimeID(6), AnimeID(10)], tmdb);
            Assert.Equal(tmdbBefore + tmdb.Length, repository.CountMissingLinks(MetadataSource.TMDB, _neverLinked));
        }
        finally
        {
            Remove(rows);
        }

        Assert.Equal(pluginBefore, repository.CountMissingLinks(_plugin, _neverLinked));
    }

    [Fact]
    public void NoTypeLeftOutCountsEveryTypeButStillNeedsTheAnime()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var repository = fixture.Services.GetRequiredService<AnimeSeriesRepository>();
        var rows = Seed();
        try
        {
            var seeded = Enumerable.Range(1, _anime.Length).Select(AnimeID).ToHashSet();
            var plugin = repository.GetAnimeIDsMissingLinks(_plugin, []).Where(seeded.Contains).Order().ToArray();

            Assert.Equal([AnimeID(1), AnimeID(5), AnimeID(7), AnimeID(9), AnimeID(10), AnimeID(11)], plugin);
        }
        finally
        {
            Remove(rows);
        }
    }

    [Fact]
    public void ASourceWithoutANumberIsCountedWithoutGivingItOne()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var repository = fixture.Services.GetRequiredService<AnimeSeriesRepository>();
        var source = MetadataSource.Parse("mlc-numberless");
        Assert.False(MetadataNumberRegistry.TryGetNumber(source, out _));
        var rows = Seed();
        try
        {
            var seeded = Enumerable.Range(1, _anime.Length).Select(AnimeID).ToHashSet();
            var missing = repository.GetAnimeIDsMissingLinks(source, _neverLinked).Where(seeded.Contains).Order().ToArray();

            // It has no rows, so only the anime types and the vetoes leave a series out.
            Assert.Equal([AnimeID(1), AnimeID(2), AnimeID(3), AnimeID(4), AnimeID(6), AnimeID(7), AnimeID(10), AnimeID(11)], missing);
            Assert.False(MetadataNumberRegistry.TryGetNumber(source, out _));
        }
        finally
        {
            Remove(rows);
        }
    }

    #endregion
}
