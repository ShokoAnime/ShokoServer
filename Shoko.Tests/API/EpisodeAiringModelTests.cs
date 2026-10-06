using System;
using Moq;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;
using Xunit;

using EpisodeAiringDto = Shoko.Server.API.v3.Models.Airing.EpisodeAiring;

namespace Shoko.Tests.API;

/// <summary>
/// Covers how an airing no source lists the episode for yet goes over the
/// wire: as a regular episode at its AniDB place, of its anime.
/// </summary>
public class EpisodeAiringModelTests
{
    [Fact]
    public void AnUnresolvedAiringIsARegularEpisodeAtItsAnidbPlace()
    {
        var airing = new Mock<IEpisodeAiring>();
        airing.SetupGet(entry => entry.ID).Returns(Guid.NewGuid());
        airing.SetupGet(entry => entry.Tracks).Returns([]);
        airing.SetupGet(entry => entry.SequenceNumber).Returns(2);
        airing.SetupGet(entry => entry.EpisodeNumber).Returns(14);
        airing.SetupGet(entry => entry.AnidbAnimeID).Returns(900);
        airing.SetupGet(entry => entry.AnidbEpisodeNumber).Returns(13);

        var dto = new EpisodeAiringDto(airing.Object);

        Assert.Equal((false, 2, EpisodeType.Episode, 13, 900), (dto.IsResolved, dto.SequenceNumber, dto.Type, dto.Number, dto.IDs.AnidbAnime));
    }
}
