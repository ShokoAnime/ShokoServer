using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Video.Relocation;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the guards <see cref="VideoRelocationService.DirectlyRelocateFile"/> applies before it
/// moves a user's file. Each one is the last thing standing between a bad request and a file being
/// written somewhere it should not be, and none of them had a test.
/// </summary>
/// <remarks>
/// The file system is reached through the injected <see cref="IFileSystemHelpers"/>, so these run
/// against a mock and never touch a disk — a rejected request is proven by the mock never being
/// asked to move anything.
/// </remarks>
[Collection(nameof(RepoFactoryCollection))]
public class VideoRelocationGuardTests
{
    private const int VideoID = 1;
    private const int SourceFolderID = 1;
    private const int DestinationFolderID = 2;
    private const string SourcePath = "/media/anime";
    private const string DestinationPath = "/media/sorted";
    private const string RelativePath = "Show/episode.mkv";

    private static ShokoManagedFolder Folder(int id, string path, bool isDropSource = true, bool isDropDestination = false)
        => new() { ID = id, Name = $"folder-{id}", Path = path, IsDropSource = isDropSource, IsDropDestination = isDropDestination };

    private sealed class Harness : IDisposable
    {
        public Mock<IFileSystemHelpers> FileSystem { get; } = new(MockBehavior.Loose);

        public VideoRelocationService Service { get; }

        public VideoLocal_Place Place { get; }

        private readonly RepoFactoryScope _scope;

        public Harness(ShokoManagedFolder sourceFolder, ShokoManagedFolder destinationFolder)
        {
            Place = new VideoLocal_Place { ID = 1, VideoID = VideoID, ManagedFolderID = sourceFolder.ID, RelativePath = RelativePath };

            _scope = new RepoFactoryScope()
                .With<VideoLocalRepository, int, VideoLocal>(v => v.VideoLocalID,
                    [new VideoLocal { VideoLocalID = VideoID, Hash = "abc", FileSize = 100 }])
                .With<ShokoManagedFolderRepository, int, ShokoManagedFolder>(f => f.ID, [sourceFolder, destinationFolder])
                .With<VideoLocal_PlaceRepository, int, VideoLocal_Place>(p => p.ID, [Place]);

            // The file is present unless a test says otherwise.
            FileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);

            Service = new VideoRelocationService(
                NullLogger<VideoRelocationService>.Instance,
                serviceProvider: null!,
                pluginManager: null!,
                settingsProvider: null!,
                schedulerFactory: null!,
                configurationService: null!,
                fileWatcherService: null!,
                videoLocalPlace: null!,
                storedRelocationPresetRepository: null!,
                fileNameHash: null!,
                managedFolders: null!,
                fileSystemHelpers: FileSystem.Object);
        }

        /// <summary>Asserts that nothing was written to, moved on, or removed from disk.</summary>
        public void AssertNothingWasMoved()
        {
            FileSystem.Verify(f => f.MoveFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            FileSystem.Verify(f => f.DeleteFile(It.IsAny<string>()), Times.Never);
            FileSystem.Verify(f => f.CreateDirectory(It.IsAny<string>()), Times.Never);
        }

        public void Dispose() => _scope.Dispose();
    }

    private static Harness Create(bool sourceIsDropSource = true, bool sourceIsDropDestination = false)
        => new(Folder(SourceFolderID, SourcePath, sourceIsDropSource, sourceIsDropDestination), Folder(DestinationFolderID, DestinationPath, false, true));

    private static DirectlyRelocateRequest Request(ShokoManagedFolder? folder, string? relativePath, bool allowInsideDestination = true)
        => new()
        {
            ManagedFolder = folder,
            RelativePath = relativePath,
            AllowRelocationInsideDestination = allowInsideDestination,
        };

    #region Request validation

    [Fact]
    public async Task ARequestWithoutAManagedFolderIsRejected()
    {
        using var harness = Create();

        var response = await harness.Service.DirectlyRelocateFile(harness.Place, Request(null, RelativePath));

        Assert.False(response.Success);
        harness.AssertNothingWasMoved();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ARequestWithoutARelativePathIsRejected(string? relativePath)
    {
        using var harness = Create();

        var response = await harness.Service.DirectlyRelocateFile(
            harness.Place, Request(Folder(DestinationFolderID, DestinationPath), relativePath));

        Assert.False(response.Success);
        harness.AssertNothingWasMoved();
    }

    [Fact]
    public async Task ACancelledRequestIsRejected()
    {
        using var harness = Create();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var request = Request(Folder(DestinationFolderID, DestinationPath), RelativePath) with { CancellationToken = cancellation.Token };
        var response = await harness.Service.DirectlyRelocateFile(harness.Place, request);

        Assert.False(response.Success);
        harness.AssertNothingWasMoved();
    }

    #endregion

    #region Escaping the managed folder

    [Theory]
    [InlineData("../outside/episode.mkv")]
    [InlineData("Show/../../outside/episode.mkv")]
    [InlineData("../../../etc/episode.mkv")]
    public async Task ARelativePathThatClimbsOutOfTheManagedFolderIsRejected(string relativePath)
    {
        using var harness = Create();

        var response = await harness.Service.DirectlyRelocateFile(
            harness.Place, Request(Folder(DestinationFolderID, DestinationPath), relativePath));

        Assert.False(response.Success);
        harness.AssertNothingWasMoved();
    }

    [Fact]
    public async Task ARelativePathLeadingIntoASiblingFolderWithASharedPrefixIsRejected()
    {
        using var harness = Create();

        // "/media/anime" and "/media/animeX" share a prefix. The containment check only holds
        // because ShokoManagedFolder.Path always ends in a directory separator; a plain prefix
        // comparison would let this through and write the file outside the managed folder.
        var response = await harness.Service.DirectlyRelocateFile(
            harness.Place, Request(Folder(DestinationFolderID, "/media/anime"), "../animeX/episode.mkv"));

        Assert.False(response.Success);
        Assert.Contains("outside the managed folder", response.Error?.Message ?? string.Empty);
        harness.AssertNothingWasMoved();
    }

    [Fact]
    public void AManagedFolderPathAlwaysEndsInASeparator()
    {
        // The containment check above depends on this, so it is pinned here rather than left to
        // chance in ShokoManagedFolder.
        Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), Folder(SourceFolderID, SourcePath).Path);
    }

    [Fact]
    public async Task ARelativePathStayingInsideTheManagedFolderPassesTheContainmentCheck()
    {
        using var harness = Create();

        var response = await harness.Service.DirectlyRelocateFile(
            harness.Place, Request(Folder(DestinationFolderID, DestinationPath), "Show/Season 1/../episode.mkv"));

        // It may still fail later for other reasons, but not for leaving the folder.
        Assert.DoesNotContain("outside the managed folder", response.Error?.Message ?? string.Empty);
    }

    #endregion

    #region Drop folder rules

    [Fact]
    public async Task AFileInAnExcludedFolderIsNotRelocated()
    {
        using var harness = Create(sourceIsDropSource: false, sourceIsDropDestination: false);

        var response = await harness.Service.DirectlyRelocateFile(
            harness.Place, Request(Folder(DestinationFolderID, DestinationPath), RelativePath));

        Assert.False(response.Success);
        harness.AssertNothingWasMoved();
    }

    [Fact]
    public async Task AFileInADropDestinationIsNotRelocatedWhenRelocatingInsideDestinationsIsDisabled()
    {
        using var harness = Create(sourceIsDropSource: false, sourceIsDropDestination: true);

        var response = await harness.Service.DirectlyRelocateFile(
            harness.Place, Request(Folder(DestinationFolderID, DestinationPath), RelativePath, allowInsideDestination: false));

        Assert.False(response.Success);
        harness.AssertNothingWasMoved();
    }

    [Fact]
    public async Task AFileInAFolderThatIsBothSourceAndDestinationIsAllowedThrough()
    {
        using var harness = Create(sourceIsDropSource: true, sourceIsDropDestination: true);

        var response = await harness.Service.DirectlyRelocateFile(
            harness.Place, Request(Folder(DestinationFolderID, DestinationPath), RelativePath, allowInsideDestination: false));

        Assert.DoesNotContain("drop destination", response.Error?.Message ?? string.Empty);
    }

    #endregion

    #region File system state

    [Fact]
    public async Task AMissingSourceFileIsReportedRatherThanMoved()
    {
        using var harness = Create();
        harness.FileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        var response = await harness.Service.DirectlyRelocateFile(
            harness.Place, Request(Folder(DestinationFolderID, DestinationPath), RelativePath));

        Assert.False(response.Success);
        harness.AssertNothingWasMoved();
    }

    [Fact]
    public async Task TheSourceFileIsLookedForAtItsCurrentLocation()
    {
        using var harness = Create();
        harness.FileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        await harness.Service.DirectlyRelocateFile(
            harness.Place, Request(Folder(DestinationFolderID, DestinationPath), RelativePath));

        harness.FileSystem.Verify(f => f.FileExists(Path.Combine(SourcePath, RelativePath)), Times.AtLeastOnce);
    }

    #endregion
}
