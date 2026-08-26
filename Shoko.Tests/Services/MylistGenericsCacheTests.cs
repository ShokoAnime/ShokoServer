using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Shoko.Server.Services.Mylist;
using Xunit;

namespace Shoko.Tests.Services;

public class MylistGenericsCacheTests
{
    [Fact]
    public void Gaps_RoundTripTheSameSet()
    {
        var fileIDs = new HashSet<int> { 4218400, 66512, 66513, 66514, 1, 999999 };

        Assert.Equal(fileIDs.Order(), MylistGenericsCache.FromGaps(MylistGenericsCache.ToGaps(fileIDs)).Order());
    }

    /// <summary>
    /// The whole point of the encoding: consecutive IDs cost one digit each
    /// rather than seven.
    /// </summary>
    [Fact]
    public void Gaps_AreTheDistanceFromThePreviousID()
    {
        var fileIDs = new HashSet<int> { 300, 100, 101, 105 };

        Assert.Equal([100, 1, 4, 195], MylistGenericsCache.ToGaps(fileIDs));
    }

    [Fact]
    public void Gaps_HandleAnEmptySet()
    {
        Assert.Empty(MylistGenericsCache.ToGaps([]));
        Assert.Empty(MylistGenericsCache.FromGaps([]));
    }

    [Fact]
    public void Gaps_SurviveALargeSparseSet()
    {
        var fileIDs = Enumerable.Range(0, 50_000).Select(index => index * 7 + 66512).ToHashSet();

        Assert.Equal(fileIDs.Order(), MylistGenericsCache.FromGaps(MylistGenericsCache.ToGaps(fileIDs)).Order());
    }

    /// <summary>
    /// The reader has to agree with the writer, and .NET maps
    /// <c>CompressionLevel.Optimal</c> to a Brotli quality that comes out
    /// larger than gzip — so both ends are pinned here.
    /// </summary>
    [Theory]
    [InlineData(CompressionLevel.Optimal)]
    [InlineData(CompressionLevel.SmallestSize)]
    internal void CompressedJsonFile_RoundTripsACache(CompressionLevel level)
    {
        var path = Path.Combine(Path.GetTempPath(), $"shoko-cache-{Guid.NewGuid():N}.bin");
        try
        {
            var fileIDs = Enumerable.Range(0, 20_000).Select(index => index * 3 + 66512).ToHashSet();
            CompressedJsonFile.Write(path, MylistGenericsCache.ToGaps(fileIDs), level);

            Assert.Equal(fileIDs.Order(), MylistGenericsCache.FromGaps(CompressedJsonFile.Read<List<int>>(path)!).Order());
            // and it is actually compressed, not just written out
            Assert.True(new FileInfo(path).Length < 20_000);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
