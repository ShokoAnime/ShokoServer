using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Scheduling;
using Xunit;

namespace Shoko.Tests.Scheduling;

/// <summary>
/// Covers the priorities chosen at queue time: by file size, by metadata entry
/// kind and by an image's owner and type, each boosted for something new and
/// for a prioritized job.
/// </summary>
public class JobPrioritiesTests
{
    #region Constants

    private const long MiB = 1024L * 1024L;

    private const long GiB = 1024L * MiB;

    #endregion

    #region File Size

    [Theory]
    [InlineData(200 * MiB - 1, 40)]
    [InlineData(200 * MiB, 30)]
    [InlineData(GiB - 1, 30)]
    [InlineData(GiB, 20)]
    [InlineData(GiB + GiB / 2 - 1, 20)]
    [InlineData(GiB + GiB / 2, 10)]
    [InlineData(4 * GiB - 1, 10)]
    [InlineData(4 * GiB, 0)]
    [InlineData(null, 20)]
    public void ForFileSize_RanksSmallerFilesFirst(long? fileSize, int expected)
        => Assert.Equal(expected, JobPriorities.ForFileSize(fileSize, isNew: false, prioritize: false));

    [Fact]
    public void ForFileSize_ANewFile_RanksAboveAKnownFileOfAnySize()
        => Assert.True(JobPriorities.ForFileSize(4 * GiB, isNew: true, prioritize: false) > JobPriorities.ForFileSize(1, isNew: false, prioritize: false));

    [Fact]
    public void ForFileSize_APrioritizedKnownFile_IsLevelWithANewFileOfTheSameTier()
        => Assert.Equal(
            JobPriorities.ForFileSize(GiB, isNew: true, prioritize: false),
            JobPriorities.ForFileSize(GiB, isNew: false, prioritize: true)
        );

    #endregion

    #region Metadata Entries

    [Theory]
    [InlineData("series", 30)]
    [InlineData("movie", 30)]
    [InlineData("collection", 30)]
    [InlineData("season", 20)]
    [InlineData("episode", 20)]
    [InlineData("ordering", 10)]
    [InlineData("creator", 0)]
    [InlineData("character", 0)]
    [InlineData("studio", 0)]
    [InlineData("network", 0)]
    public void ForMetadataEntry_RanksByKind(string kind, int expected)
        => Assert.Equal(expected, JobPriorities.ForMetadataEntry(MetadataEntityType.Get(kind), isNew: false, prioritize: false));

    [Fact]
    public void ForMetadataEntry_ANewEntry_RanksAboveAKnownOneOfTheSameTier()
        => Assert.True(
            JobPriorities.ForMetadataEntry(MetadataEntityType.Series, isNew: true, prioritize: false) >
            JobPriorities.ForMetadataEntry(MetadataEntityType.Series, isNew: false, prioritize: false)
        );

    [Fact]
    public void ForMetadataEntry_APrioritizedKnownEntry_IsLevelWithANewOne()
        => Assert.Equal(
            JobPriorities.ForMetadataEntry(MetadataEntityType.Episode, isNew: true, prioritize: false),
            JobPriorities.ForMetadataEntry(MetadataEntityType.Episode, isNew: false, prioritize: true)
        );

    [Fact]
    public void ForMetadataEntry_ThePrioritizedNewBoost_AddsBoth()
        => Assert.Equal(
            JobPriorities.ForMetadataEntry(MetadataEntityType.Movie, isNew: false, prioritize: false) + 100,
            JobPriorities.ForMetadataEntry(MetadataEntityType.Movie, isNew: true, prioritize: true)
        );

    #endregion

    #region Images

    [Theory]
    [InlineData("series", ImageEntityType.Primary, 30)]
    [InlineData("movie", ImageEntityType.Backdrop, 30)]
    [InlineData("collection", ImageEntityType.Logo, 30)]
    [InlineData("series", ImageEntityType.Disc, 10)]
    [InlineData("season", ImageEntityType.Primary, 20)]
    [InlineData("episode", ImageEntityType.Backdrop, 20)]
    [InlineData("creator", ImageEntityType.Primary, 0)]
    [InlineData("studio", ImageEntityType.Logo, 0)]
    [InlineData(null, ImageEntityType.Primary, 10)]
    public void ForImage_RanksByOwnerAndType(string? kind, ImageEntityType imageType, int expected)
        => Assert.Equal(expected, JobPriorities.ForImage(kind is null ? null : MetadataEntityType.Get(kind), imageType, isNew: false, prioritize: false));

    [Fact]
    public void ForImage_ANewImage_RanksAboveAKnownOneOfTheSameTier()
        => Assert.True(
            JobPriorities.ForImage(MetadataEntityType.Creator, ImageEntityType.Primary, isNew: true, prioritize: false) >
            JobPriorities.ForImage(MetadataEntityType.Creator, ImageEntityType.Primary, isNew: false, prioritize: false)
        );

    [Fact]
    public void ForImage_APrioritizedKnownImage_IsLevelWithANewOne()
        => Assert.Equal(
            JobPriorities.ForImage(MetadataEntityType.Series, ImageEntityType.Primary, isNew: true, prioritize: false),
            JobPriorities.ForImage(MetadataEntityType.Series, ImageEntityType.Primary, isNew: false, prioritize: true)
        );

    #endregion
}
