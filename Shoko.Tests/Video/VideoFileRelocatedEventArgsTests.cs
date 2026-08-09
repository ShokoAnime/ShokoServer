using System.IO;
using Moq;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Events;
using Xunit;

namespace Shoko.Tests.Video;

public class VideoFileRelocatedEventArgsTests
{
    [Fact]
    public void PreviousPath_DoesNotDuplicateSeparatorAtManagedFolderBoundary()
    {
        var folderPath = Path.Join("library", "anime") + Path.DirectorySeparatorChar;
        var folder = new Mock<IManagedFolder>();
        folder.SetupGet(a => a.Path).Returns(folderPath);

        var eventArgs = new VideoFileRelocatedEventArgs(
            "new-file.mkv", folder.Object, "series/old-file.mkv", folder.Object,
            Mock.Of<IVideoFile>(), Mock.Of<IVideo>(), [], [], []);

        Assert.Equal(Path.Join(folderPath, "series", "old-file.mkv"), eventArgs.PreviousPath);
    }
}
