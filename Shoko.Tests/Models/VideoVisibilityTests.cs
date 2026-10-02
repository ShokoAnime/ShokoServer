using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Models;

/// <summary>
/// Covers how a user with restricted tags sees files where they are listed or acted on: a file is
/// kept when it has no series, or when any of its series is visible to the user.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class VideoVisibilityTests
{
    private const string HiddenTag = "secret";

    #region World

    /// <summary>
    /// A file linked to a visible and a hidden anime, one linked to the hidden anime only, and one
    /// linked to nothing.
    /// </summary>
    private sealed class World : IDisposable
    {
        private readonly RepoFactoryScope _scope;

        public VideoLocal MixedVideo { get; } = Video(1, "mixed");

        public VideoLocal HiddenVideo { get; } = Video(2, "hidden");

        public VideoLocal UnlinkedVideo { get; } = Video(3, "unlinked");

        public JMMUser User { get; } = new() { JMMUserID = 1, HideCategories = HiddenTag };

        public VideoLocalRepository Videos { get; }

        public World()
        {
            Videos = CachedRepo.Build<VideoLocalRepository, int, VideoLocal>(video => video.VideoLocalID, [MixedVideo, HiddenVideo, UnlinkedVideo]);
            _scope = new RepoFactoryScope()
                .Set(Videos)
                .With<JMMUserRepository, int, JMMUser>(user => user.JMMUserID, [User])
                .With<CrossRef_File_EpisodeRepository, int, CrossRef_File_Episode>(xref => xref.CrossRef_File_EpisodeID, [
                    Link(1, MixedVideo, animeID: 1),
                    Link(2, MixedVideo, animeID: 2),
                    Link(3, HiddenVideo, animeID: 2),
                ])
                .With<AniDB_AnimeRepository, int, AniDB_Anime>(anime => anime.AniDB_AnimeID, [
                    new() { AniDB_AnimeID = 1, AnimeID = 1, AllTags = string.Empty },
                    new() { AniDB_AnimeID = 2, AnimeID = 2, AllTags = HiddenTag },
                ]);
        }

        private static VideoLocal Video(int id, string hash)
            => new() { VideoLocalID = id, Hash = hash, FileSize = id, DateTimeCreated = DateTime.Today.AddDays(-id) };

        private static CrossRef_File_Episode Link(int id, VideoLocal video, int animeID)
            => new() { CrossRef_File_EpisodeID = id, Hash = video.Hash, FileSize = video.FileSize, AnimeID = animeID, EpisodeID = id };

        public void Dispose()
            => _scope.Dispose();
    }

    private sealed class NoopVideoAction : VideoAction
    {
        public override string Name => "Noop";

        public override ActionPermission Permission => ActionPermission.User;

        public override Task Execute(CancellationToken token = default) => Task.CompletedTask;
    }

    #endregion

    [Fact]
    public void AListingKeepsFilesWithAVisibleSeriesOrNone()
    {
        using var world = new World();

        var listed = world.Videos.GetMostRecentlyAdded(-1, world.User.JMMUserID);

        Assert.Equal([1, 3], listed.Select(video => video.VideoLocalID));
    }

    [Fact]
    public void AFileActionNeedsOnlyOneVisibleSeries()
    {
        using var world = new World();
        var action = new NoopVideoAction();

        Assert.Null(ActionService.CheckVisible(action, world.MixedVideo, world.User));
        Assert.Null(ActionService.CheckVisible(action, world.UnlinkedVideo, world.User));
        Assert.NotNull(ActionService.CheckVisible(action, world.HiddenVideo, world.User));
    }
}
