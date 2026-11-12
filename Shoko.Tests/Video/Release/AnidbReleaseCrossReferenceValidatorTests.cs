using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Release;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Video.Release;

/// <summary>
///   The core's AniDB validator: what it refuses, and how it fills in the
///   anime of an episode.
/// </summary>
public class AnidbReleaseCrossReferenceValidatorTests
{
    #region Helpers

    private readonly Mock<IUDPConnectionHandler> _udp = new();

    private readonly Mock<IRequestFactory> _requests = new(MockBehavior.Strict);

    private AnidbReleaseCrossReferenceValidator Build(params AniDB_Episode[] episodes)
        => new(
            NullLogger<AnidbReleaseCrossReferenceValidator>.Instance,
            _udp.Object,
            _requests.Object,
            CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(episode => episode.AniDB_EpisodeID, episodes)
        );

    private static Task<ReleaseCrossReferenceValidationResult?> Validate(AnidbReleaseCrossReferenceValidator validator, int index, params IReleaseVideoCrossReference[] crossReferences)
        => validator.Validate(
            new() { Video = Mock.Of<IVideo>(), Release = Mock.Of<IReleaseInfo>(), CrossReferences = crossReferences, Index = index },
            TestContext.Current.CancellationToken
        );

    private static ReleaseVideoCrossReference Corrected(ReleaseCrossReferenceValidationResult result)
        => new() { IDs = [.. Assert.IsAssignableFrom<IReadOnlyList<MetadataGuid>>(result.CorrectedIDs)] };

    #endregion

    [Fact]
    public async Task AnEpisodeWithoutItsAnime_GetsTheAnimeOfTheLocalEpisode()
    {
        var validator = Build(new AniDB_Episode { AniDB_EpisodeID = 7, EpisodeID = 70, AnimeID = 3 });

        var result = await Validate(validator, 0, ReleaseVideoCrossReference.ForAniDB(70));

        Assert.True(result!.IsValid);
        Assert.Equal(3, Corrected(result).AnidbAnimeID);
        Assert.Equal(70, Corrected(result).AnidbEpisodeID);
    }

    [Fact]
    public async Task AnEpisodeWithoutItsAnime_TakesItFromAnotherCrossReferenceOfTheSameEpisode()
    {
        var validator = Build();

        var result = await Validate(validator, 0, ReleaseVideoCrossReference.ForAniDB(70, null, 0, 50), ReleaseVideoCrossReference.ForAniDB(70, 9, 50, 100));

        Assert.Equal(9, Corrected(result!).AnidbAnimeID);
    }

    [Fact]
    public async Task WhileBanned_AnUnknownEpisodeIsValidButUncorrected()
    {
        _udp.SetupGet(udp => udp.IsBanned).Returns(true);
        var validator = Build();

        var result = await Validate(validator, 0, ReleaseVideoCrossReference.ForAniDB(70));

        Assert.True(result!.IsValid);
        Assert.False(result.IsCorrected);
    }

    [Theory]
    [InlineData("anidb://series/3")]
    [InlineData("anidb://episode/0")]
    [InlineData("anidb://episode/abc")]
    [InlineData("anidb://episode/1,anidb://episode/2")]
    [InlineData("anidb://episode/1,anidb://series/2,anidb://series/3")]
    public async Task MalformedAnidbIDs_AreInvalid(string ids)
    {
        var validator = Build();
        var xref = new ReleaseVideoCrossReference { IDs = [.. ids.Split(',').Select(id => MetadataGuid.Parse(id))] };

        var result = await Validate(validator, 0, xref);

        Assert.False(result!.IsValid);
    }
}
