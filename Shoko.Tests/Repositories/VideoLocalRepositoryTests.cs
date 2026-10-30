using System;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Repositories;

/// <summary>
///   Finding the files of an anime through its file cross-references.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class VideoLocalRepositoryTests
{
    [Fact]
    public void FilesOfAnAnime_AreEachListedOnce_AndEmptyLinksAreSkipped()
    {
        var video = new VideoLocal { VideoLocalID = 1, Hash = "ABC", FileSize = 100, DateTimeCreated = DateTime.Today };
        var videos = CachedRepo.Build<VideoLocalRepository, int, VideoLocal>(v => v.VideoLocalID, [video]);
        using var scope = new RepoFactoryScope()
            .Set(videos)
            .With<CrossRef_File_EpisodeRepository, int, CrossRef_File_Episode>(xref => xref.CrossRef_File_EpisodeID, [
                // One file covering two episodes of the anime.
                new() { CrossRef_File_EpisodeID = 1, Hash = "ABC", FileSize = 100, AnimeID = 7, EpisodeID = 1 },
                new() { CrossRef_File_EpisodeID = 2, Hash = "ABC", FileSize = 100, AnimeID = 7, EpisodeID = 2 },
                // A link left without a file size, which names no file.
                new() { CrossRef_File_EpisodeID = 3, Hash = "DEF", FileSize = 0, AnimeID = 7, EpisodeID = 3 },
            ]);

        Assert.Same(video, Assert.Single(videos.GetByAniDBAnimeID(7)));
    }
}
