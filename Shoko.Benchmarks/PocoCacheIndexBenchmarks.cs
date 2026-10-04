using BenchmarkDotNet.Attributes;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Utilities;

namespace Benchmarks;

/// <summary>
///   What the cached repositories' indexes cost to build and to read, on rows shaped like <c>ShokoImage_Entity</c>.
/// </summary>
/// <remarks>
///   The rows hold Guid image IDs, about one in ten shared, and between 1 and 20 images per entity. The build runs the
///   five indexes of <c>ShokoImage_EntityRepository</c> over a fresh cache.
/// </remarks>
[BenchmarkCategory("PocoCache")]
[MemoryDiagnoser]
public class PocoCacheIndexBenchmarks
{
    private const int LookupCount = 4096;

    private Row[] _rows = null!;

    private Row[] _probes = null!;

    private PocoIndex<int, Row, Guid> _imageIDs = null!;

    private PocoIndex<int, Row, (MetadataSource, MetadataEntityType, string)> _entities = null!;

    [Params(700_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _rows = Build(RowCount);
        var random = new Random(7);
        _probes = [.. Enumerable.Range(0, LookupCount).Select(_ => _rows[random.Next(_rows.Length)])];

        var cache = new PocoCache<int, Row>(_rows, row => row.ID);
        _imageIDs = cache.CreateIndex(row => row.ImageID);
        _entities = cache.CreateIndex(row => (row.EntitySource, row.EntityType, row.EntityID));
    }

    [Benchmark]
    public PocoCache<int, Row> BuildIndexes()
    {
        var cache = new PocoCache<int, Row>(_rows, row => row.ID);
        cache.CreateIndex(row => row.ImageID);
        cache.CreateIndex(row => row.PrimaryImageID);
        cache.CreateIndex(row => (row.EntitySource, row.EntityType));
        cache.CreateIndex(row => (row.EntitySource, row.EntityType, row.EntityID));
        cache.CreateIndex(row => (row.EntitySource, row.EntityType, row.EntityID, row.ImageType));
        return cache;
    }

    [Benchmark(OperationsPerInvoke = LookupCount)]
    public int GetMultipleByImageID()
    {
        var count = 0;
        foreach (var probe in _probes)
            count += _imageIDs.GetMultiple(probe.ImageID).Count;
        return count;
    }

    [Benchmark(OperationsPerInvoke = LookupCount)]
    public int GetMultipleByEntity()
    {
        var count = 0;
        foreach (var probe in _probes)
            count += _entities.GetMultiple((probe.EntitySource, probe.EntityType, probe.EntityID)).Count;
        return count;
    }

    /// <summary>
    ///   Builds rows with a fixed seed, so every run indexes the same data.
    /// </summary>
    /// <param name="count">The number of rows.</param>
    /// <returns>The rows.</returns>
    private static Row[] Build(int count)
    {
        var random = new Random(42);
        MetadataSource[] sources = [MetadataSource.AniDB, MetadataSource.AniDB, MetadataSource.TMDB, MetadataSource.Shoko];
        MetadataEntityType[] types = [MetadataEntityType.Series, MetadataEntityType.Episode, MetadataEntityType.Episode, MetadataEntityType.Movie];
        ImageEntityType[] imageTypes = [ImageEntityType.Primary, ImageEntityType.Primary, ImageEntityType.Backdrop, ImageEntityType.Logo];
        var rows = new Row[count];
        var id = 0;
        var entity = 0;
        while (id < count)
        {
            var images = 1 + random.Next(20);
            var source = sources[random.Next(sources.Length)];
            var type = types[random.Next(types.Length)];
            var entityID = (entity++).ToString();
            for (var image = 0; image < images && id < count; image++, id++)
            {
                var imageID = id > 0 && random.Next(10) is 0 ? rows[random.Next(id)].ImageID : Guid.NewGuid();
                var primaryImageID = id > 0 && random.Next(5) is 0 ? rows[random.Next(id)].PrimaryImageID : imageID;
                rows[id] = new(id + 1, imageID, primaryImageID, imageTypes[random.Next(imageTypes.Length)], source, type, entityID);
            }
        }

        return rows;
    }

    /// <summary>
    ///   A row shaped like <c>ShokoImage_Entity</c>'s indexed columns.
    /// </summary>
    /// <param name="ID">The primary key.</param>
    /// <param name="ImageID">The image.</param>
    /// <param name="PrimaryImageID">The primary image.</param>
    /// <param name="ImageType">The image type.</param>
    /// <param name="EntitySource">The linked entity's source.</param>
    /// <param name="EntityType">The linked entity's kind.</param>
    /// <param name="EntityID">The linked entity's ID.</param>
    public sealed record Row(
        int ID,
        Guid ImageID,
        Guid PrimaryImageID,
        ImageEntityType ImageType,
        MetadataSource EntitySource,
        MetadataEntityType EntityType,
        string EntityID
    );
}
