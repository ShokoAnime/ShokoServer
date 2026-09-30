using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers how <see cref="UserDataService"/> keeps a user's stats row for a
/// group: several stats updates of one group running at once add the row only
/// once, as the database's unique index on user and group demands, and the row
/// sums its series and is saved only when that changes.
/// </summary>
public class UserDataServiceGroupTests
{
    private const int UserID = 3;

    private const int GroupID = 11;

    #region Helpers

    private sealed class Harness
    {
        private readonly Lock _gate = new();

        private int _nextID;

        public Mock<AnimeGroup_UserRepository> Groups { get; }

        public AnimeSeries_UserRepository Series { get; }

        public UserDataService Service { get; }

        public IShokoGroup Group { get; }

        public IUser User { get; }

        public Harness(params AnimeSeries_User[] seriesRows)
        {
            Groups = CachedRepo.BuildWritable<AnimeGroup_UserRepository, int, AnimeGroup_User>(row => row.AnimeGroup_UserID);
            // Adding a row takes a while and refuses a second one for the same
            // user and group, as the database's unique index does.
            Groups.Setup(repository => repository.Save(It.IsAny<AnimeGroup_User>())).Callback<AnimeGroup_User>(row =>
            {
                if (row.AnimeGroup_UserID is 0)
                {
                    Thread.Sleep(20);
                    lock (_gate)
                    {
                        if (Groups.Object.GetByUserAndGroupID(row.JMMUserID, row.AnimeGroupID) is not null)
                            throw new InvalidOperationException("UNIQUE constraint failed: AnimeGroup_User.JMMUserID, AnimeGroup_User.AnimeGroupID");
                        row.AnimeGroup_UserID = ++_nextID;
                    }
                }

                Groups.Object.Cache.Update(row);
            });
            Series = CachedRepo.Build<AnimeSeries_UserRepository, int, AnimeSeries_User>(row => row.AnimeSeries_UserID, seriesRows);

            var group = new Mock<IShokoGroup>();
            group.SetupGet(value => value.LocalID).Returns(GroupID);
            group.SetupGet(value => value.AllSeries).Returns([]);
            Group = group.Object;
            var user = new Mock<IUser>();
            user.SetupGet(value => value.LocalID).Returns(UserID);
            User = user.Object;

            Service = new UserDataService(
                NullLogger<UserDataService>.Instance,
                settingsProvider: null!,
                schedulerFactory: null!,
                serviceProvider: null!,
                videoUserDataRepository: null!,
                episodeUserDataRepository: null!,
                seriesUserDataRepository: Series,
                groupUserDataRepository: Groups.Object,
                userRepository: null!
            );
        }

        public IShokoSeries SeriesWithID(int seriesID)
        {
            var series = new Mock<IShokoSeries>();
            series.SetupGet(value => value.LocalID).Returns(seriesID);
            return series.Object;
        }
    }

    #endregion

    #region Creating the row

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StatsUpdatesOfOneGroupAtOnceAddItsRowOnce(bool savedByTheCaller)
    {
        var harness = new Harness();
        Action<AnimeGroup_User, bool, bool>? save = savedByTheCaller
        ? (row, _, updated) =>
        {
            if (updated)
                harness.Groups.Object.Save(row);
        }
        : null;

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => harness.Service.UpdateWatchedStats(harness.Group, harness.User, [], save))));

        var row = Assert.Single(harness.Groups.Object.GetAll());
        Assert.Equal((UserID, GroupID), (row.JMMUserID, row.AnimeGroupID));
    }

    #endregion

    #region Stats

    [Fact]
    public void TheRowSumsItsSeriesAndIsSavedOnlyWhenThatChanges()
    {
        var watchedAt = new DateTime(2024, 5, 1, 20, 0, 0, DateTimeKind.Local);
        var harness = new Harness(
            new AnimeSeries_User { AnimeSeries_UserID = 1, JMMUserID = UserID, AnimeSeriesID = 21, WatchedCount = 3, WatchedEpisodeCount = 2, UnwatchedEpisodeCount = 4 },
            new AnimeSeries_User { AnimeSeries_UserID = 2, JMMUserID = UserID, AnimeSeriesID = 22, WatchedCount = 1, WatchedEpisodeCount = 1, UnwatchedEpisodeCount = 5, WatchedDate = watchedAt }
        );
        var series = new[] { harness.SeriesWithID(21), harness.SeriesWithID(22) };

        harness.Service.UpdateWatchedStats(harness.Group, harness.User, series);
        harness.Service.UpdateWatchedStats(harness.Group, harness.User, series);

        var row = Assert.Single(harness.Groups.Object.GetAll());
        Assert.Equal((4, 3, 9, watchedAt), (row.WatchedCount, row.WatchedEpisodeCount, row.UnwatchedEpisodeCount, row.WatchedDate));
        harness.Groups.Verify(repository => repository.Save(It.IsAny<AnimeGroup_User>()), Times.Once);
    }

    #endregion
}
