using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.User;
using Shoko.Abstractions.User.Enums;
using Shoko.Abstractions.User.Events;
using Shoko.Abstractions.User.Update;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Media;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers how <see cref="UserDataService"/> folds a playback update into a video's stored user
/// data. This decides whether a file counts as watched, how far through it the user is, and how
/// many times they have seen it — the state the whole watched/unwatched view of a collection is
/// built from, and it had no tests.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class UserDataServiceVideoTests
{
    private const int UserID = 7;
    private const int VideoID = 42;

    private static readonly TimeSpan s_duration = TimeSpan.FromMinutes(24);

    private sealed class Harness : IDisposable
    {
        public Mock<VideoLocal_UserRepository> Repository { get; }

        public UserDataService Service { get; }

        public IVideo Video { get; }

        public IUser User { get; }

        public VideoLocal_User? Stored => Repository.Object.GetByUserAndVideoLocalID(UserID, VideoID);

        private readonly RepoFactoryScope _scope;

        public void Dispose() => _scope.Dispose();

        public Harness(VideoLocal_User? existing, TimeSpan? duration, bool isAnidbUser)
        {
            // VideoLocal_User.ToString() resolves the video through RepoFactory, and Moq calls it
            // when rendering a failed verification. Without this a genuine assertion failure would
            // surface as a NullReferenceException from the mocking library instead.
            _scope = new RepoFactoryScope()
                .With<VideoLocalRepository, int, VideoLocal>(v => v.VideoLocalID,
                    [new VideoLocal { VideoLocalID = VideoID, Hash = "abc", FileSize = 1 }]);

            Repository = CachedRepo.BuildWritable<VideoLocal_UserRepository, int, VideoLocal_User>(
                u => u.VideoLocal_UserID, existing is null ? [] : [existing]);

            var settings = new Mock<ISettingsProvider>();
            settings.Setup(s => s.GetSettings(It.IsAny<bool>())).Returns(new ServerSettings());

            var video = new Mock<IVideo>();
            video.SetupGet(v => v.ID).Returns(VideoID);
            video.SetupGet(v => v.CrossReferences).Returns([]);
            if (duration.HasValue)
            {
                var mediaInfo = new Mock<IMediaInfo>();
                mediaInfo.SetupGet(m => m.Duration).Returns(duration.Value);
                video.SetupGet(v => v.MediaInfo).Returns(mediaInfo.Object);
            }

            Video = video.Object;

            var user = new Mock<IUser>();
            user.SetupGet(u => u.ID).Returns(UserID);
            user.SetupGet(u => u.IsAnidbUser).Returns(isAnidbUser);
            User = user.Object;

            Service = new UserDataService(
                NullLogger<UserDataService>.Instance,
                settings.Object,
                schedulerFactory: null!,
                serviceProvider: null!,
                videoUserDataRepository: Repository.Object,
                episodeUserDataRepository: null!,
                seriesUserDataRepository: null!,
                groupUserDataRepository: null!,
                userRepository: null!);
        }
    }

    private static Harness Create(VideoLocal_User? existing = null, TimeSpan? duration = null, bool isAnidbUser = false)
        => new(existing, duration ?? s_duration, isAnidbUser);

    private static VideoLocal_User Existing(DateTime? watchedDate = null, int watchedCount = 0, TimeSpan? progress = null)
        => new()
        {
            VideoLocal_UserID = 1,
            JMMUserID = UserID,
            VideoLocalID = VideoID,
            WatchedDate = watchedDate,
            WatchedCount = watchedCount,
            ProgressPosition = progress,
            LastUpdated = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Local),
        };

    #region Argument validation

    [Fact]
    public async Task SavingWithoutAUserIsRejected()
    {
        using var harness = Create();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => harness.Service.SetVideoWatchedStatus(harness.Video, null!));
    }

    [Fact]
    public async Task SavingWithoutAVideoIsRejected()
    {
        using var harness = Create();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => harness.Service.SetVideoWatchedStatus(null!, harness.User));
    }

    #endregion

    #region Marking watched and unwatched

    [Fact]
    public async Task MarkingAVideoWatchedRecordsTheWatchedDate()
    {
        using var harness = Create();
        var watchedAt = new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Local);

        var result = await harness.Service.SetVideoWatchedStatus(harness.Video, harness.User, watched: true, watchedAt: watchedAt);

        Assert.Equal(watchedAt, result.LastPlayedAt);
        Assert.Equal(watchedAt, harness.Stored!.WatchedDate);
    }

    [Fact]
    public async Task MarkingAVideoWatchedIncrementsThePlaybackCount()
    {
        using var harness = Create(Existing(watchedCount: 3));

        var result = await harness.Service.SetVideoWatchedStatus(harness.Video, harness.User);

        Assert.Equal(4, result.PlaybackCount);
    }

    [Fact]
    public async Task MarkingAnAlreadyWatchedVideoWatchedAtTheSameTimeDoesNotCountAgain()
    {
        var watchedAt = new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Local);
        using var harness = Create(Existing(watchedDate: watchedAt, watchedCount: 1));

        var result = await harness.Service.SetVideoWatchedStatus(harness.Video, harness.User, watched: true, watchedAt: watchedAt);

        // Nothing changed, so this is not a new viewing.
        Assert.Equal(1, result.PlaybackCount);
    }

    [Fact]
    public async Task MarkingAVideoUnwatchedClearsTheWatchedDate()
    {
        using var harness = Create(Existing(watchedDate: new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Local), watchedCount: 1));

        var result = await harness.Service.SetVideoWatchedStatus(harness.Video, harness.User, watched: false);

        Assert.Null(result.LastPlayedAt);
    }

    [Fact]
    public async Task MarkingAVideoUnwatchedKeepsThePlaybackCount()
    {
        using var harness = Create(Existing(watchedDate: new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Local), watchedCount: 2));

        var result = await harness.Service.SetVideoWatchedStatus(harness.Video, harness.User, watched: false);

        // Un-watching says "I have not seen this now", not "I have never seen it".
        Assert.Equal(2, result.PlaybackCount);
    }

    [Fact]
    public async Task AWatchedDateGivenInUtcIsStoredAsLocalTime()
    {
        using var harness = Create();
        var utc = new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Utc);

        var result = await harness.Service.SetVideoWatchedStatus(harness.Video, harness.User, watched: true, watchedAt: utc);

        // Stored as local, otherwise an unchanged date from a UTC source reads as a change every
        // time it is compared against the stored local one. Asserted on the kind as well as the
        // value, so this still means something when the tests run on a machine set to UTC.
        Assert.Equal(utc.ToLocalTime(), result.LastPlayedAt);
        Assert.Equal(DateTimeKind.Local, harness.Stored!.WatchedDate!.Value.Kind);
    }

    #endregion

    #region Progress

    [Fact]
    public async Task ProgressIsRecorded()
    {
        using var harness = Create();
        var progress = TimeSpan.FromMinutes(5);

        var result = await harness.Service.SaveVideoUserData(harness.Video, harness.User, new() { ProgressPosition = progress });

        Assert.Equal(progress, result.ProgressPosition);
    }

    [Fact]
    public void NegativeProgressIsRejectedByTheUpdateItself()
    {
        // The guard sits on the update object, so a bad value never reaches the service.
        Assert.Throws<ArgumentOutOfRangeException>(() => new VideoUserDataUpdate { ProgressPosition = TimeSpan.FromMinutes(-5) });
    }

    [Fact]
    public async Task ProgressBeyondTheEndCountsAsFinishedRatherThanBeingClamped()
    {
        using var harness = Create();

        var result = await harness.Service.SaveVideoUserData(harness.Video, harness.User, new() { ProgressPosition = s_duration + TimeSpan.FromMinutes(10) });

        // Anything past the end is already past the 97.5% threshold, so it is treated as watched
        // and the position reset — the clamp to the duration never comes into play here.
        Assert.NotNull(result.LastPlayedAt);
        Assert.Equal(TimeSpan.Zero, result.ProgressPosition);
    }

    [Fact]
    public async Task ProgressPastTheNearlyFinishedThresholdMarksTheVideoWatched()
    {
        using var harness = Create();

        // Anything past 97.5% counts as finished, so trailing credits do not leave it unwatched.
        var result = await harness.Service.SaveVideoUserData(harness.Video, harness.User, new() { ProgressPosition = s_duration * 0.98 });

        Assert.NotNull(result.LastPlayedAt);
        Assert.Equal(TimeSpan.Zero, result.ProgressPosition);
    }

    [Fact]
    public async Task ProgressJustBelowTheThresholdLeavesTheVideoUnwatched()
    {
        using var harness = Create();

        var result = await harness.Service.SaveVideoUserData(harness.Video, harness.User, new() { ProgressPosition = s_duration * 0.97 });

        Assert.Null(result.LastPlayedAt);
        Assert.Equal(s_duration * 0.97, result.ProgressPosition);
    }

    [Fact]
    public async Task MarkingAVideoWatchedClearsAnyStoredProgress()
    {
        using var harness = Create(Existing(progress: TimeSpan.FromMinutes(5)));

        var result = await harness.Service.SetVideoWatchedStatus(harness.Video, harness.User);

        Assert.Equal(TimeSpan.Zero, result.ProgressPosition);
    }

    [Fact]
    public async Task ProgressIsLeftAloneWhenTheDurationIsUnknown()
    {
        using var harness = new Harness(null, duration: null, isAnidbUser: false);
        var progress = TimeSpan.FromHours(99);

        var result = await harness.Service.SaveVideoUserData(harness.Video, harness.User, new() { ProgressPosition = progress });

        // With no media info there is nothing to clamp against.
        Assert.Equal(progress, result.ProgressPosition);
        Assert.Null(result.LastPlayedAt);
    }

    #endregion

    #region Playback count

    [Fact]
    public async Task AnExplicitPlaybackCountIsStored()
    {
        using var harness = Create();

        var result = await harness.Service.SaveVideoUserData(harness.Video, harness.User, new() { PlaybackCount = 5 });

        Assert.Equal(5, result.PlaybackCount);
    }

    [Fact]
    public async Task ANegativePlaybackCountIsInferredFromTheWatchedDate()
    {
        using var harness = Create(Existing(watchedDate: new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Local)));

        var result = await harness.Service.SaveVideoUserData(harness.Video, harness.User, new() { PlaybackCount = -1 });

        Assert.Equal(1, result.PlaybackCount);
    }

    [Fact]
    public async Task ANegativePlaybackCountOnAnUnwatchedVideoInfersZero()
    {
        using var harness = Create(Existing());

        var result = await harness.Service.SaveVideoUserData(harness.Video, harness.User, new() { PlaybackCount = -1 });

        Assert.Equal(0, result.PlaybackCount);
    }

    #endregion

    #region Persistence

    [Fact]
    public async Task ANewRecordIsPersisted()
    {
        using var harness = Create();

        await harness.Service.SetVideoWatchedStatus(harness.Video, harness.User);

        harness.Repository.Verify(r => r.Save(It.IsAny<VideoLocal_User>()), Times.Once);
    }

    [Fact]
    public async Task AnUpdateThatChangesNothingIsNotPersisted()
    {
        var watchedAt = new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Local);
        using var harness = Create(Existing(watchedDate: watchedAt, watchedCount: 1));

        await harness.Service.SaveVideoUserData(harness.Video, harness.User, new(), VideoUserDataSaveReason.PlaybackProgress);

        // Playback progress fires constantly; rewriting an unchanged row on every tick would be a
        // needless write per second per client.
        harness.Repository.Verify(r => r.Save(It.IsAny<VideoLocal_User>()), Times.Never);
    }

    [Fact]
    public async Task TheSavedRecordIsReturnedOnTheNextLookup()
    {
        using var harness = Create();

        var result = await harness.Service.SetVideoWatchedStatus(harness.Video, harness.User);

        Assert.Equal(result.LastPlayedAt, harness.Stored!.WatchedDate);
    }

    #endregion

    #region Events

    [Fact]
    public async Task SavingRaisesTheVideoUserDataSavedEvent()
    {
        using var harness = Create();
        VideoUserDataSavedEventArgs? captured = null;
        harness.Service.VideoUserDataSaved += (_, args) => captured = args;

        await harness.Service.SetVideoWatchedStatus(harness.Video, harness.User, reason: VideoUserDataSaveReason.UserInteraction);

        Assert.NotNull(captured);
        Assert.Equal(VideoUserDataSaveReason.UserInteraction, captured!.Reason);
        Assert.Same(harness.User, captured.User);
    }

    [Fact]
    public async Task NoEventIsRaisedWhenNothingChanged()
    {
        var watchedAt = new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Local);
        using var harness = Create(Existing(watchedDate: watchedAt, watchedCount: 1));
        var raised = false;
        harness.Service.VideoUserDataSaved += (_, _) => raised = true;

        await harness.Service.SaveVideoUserData(harness.Video, harness.User, new());

        Assert.False(raised);
    }

    [Fact]
    public async Task AFailingEventHandlerDoesNotFailTheSave()
    {
        using var harness = Create();
        harness.Service.VideoUserDataSaved += (_, _) => throw new InvalidOperationException("boom");

        var result = await harness.Service.SetVideoWatchedStatus(harness.Video, harness.User);

        // A misbehaving listener must not lose the user's watched state.
        Assert.NotNull(result.LastPlayedAt);
        Assert.NotNull(harness.Stored!.WatchedDate);
    }

    #endregion
}
