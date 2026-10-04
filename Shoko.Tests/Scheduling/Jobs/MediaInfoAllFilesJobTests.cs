using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Scheduling.Jobs;

/// <summary>
/// Covers the media info scan of every file, which ranks each file's job by
/// its size.
/// </summary>
public class MediaInfoAllFilesJobTests
{
    #region Ranking

    [Fact]
    public async Task AScan_QueuesASmallFileAheadOfALargeOne()
    {
        var videos = CachedRepo.Build<VideoLocalRepository, int, VideoLocal>(
            video => video.VideoLocalID,
            new VideoLocal { VideoLocalID = 1, FileSize = 8L * 1024 * 1024 * 1024 },
            new VideoLocal { VideoLocalID = 2, FileSize = 100L * 1024 * 1024 }
        );
        var priorities = new Dictionary<int, int>();
        var scheduler = new Mock<IQueueScheduler>();
        scheduler
            .Setup(queue => queue.EnqueueWithPriority(
                It.IsAny<Action<MediaInfoJob>?>(),
                It.IsAny<int>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()
            ))
            .Callback<Action<MediaInfoJob>?, int, DateTimeOffset?, CancellationToken>((configure, priority, _, _) =>
            {
                var job = (MediaInfoJob)RuntimeHelpers.GetUninitializedObject(typeof(MediaInfoJob));
                configure?.Invoke(job);
                priorities[job.VideoLocalID] = priority;
            })
            .Returns(Task.CompletedTask);
        var cancellation = new Mock<IJobCancellationAccessor>();
        cancellation.SetupGet(accessor => accessor.Token).Returns(CancellationToken.None);
        var progress = new Mock<IJobProgressAccessor>();
        progress.SetupGet(accessor => accessor.Progress).Returns(Mock.Of<IProgress<decimal>>());

        await new MediaInfoAllFilesJob(scheduler.Object, videos, cancellation.Object, progress.Object).Execute();

        Assert.Equal(2, priorities.Count);
        Assert.True(priorities[2] > priorities[1]);
    }

    #endregion
}
