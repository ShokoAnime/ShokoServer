using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Providers.Anilist;
using Xunit;

namespace Shoko.Tests.Providers.Anilist;

public class AnilistLinkingServiceTests
{
    private const int AnimeId = 21;

    private static (Dictionary<int, AniDB_Episode> Anidb, Dictionary<int, Anilist_Episode> Anilist) TwoEpisodes()
    {
        var anidbEp1 = new AniDB_Episode { EpisodeID = 1, EpisodeNumber = 1, EpisodeType = EpisodeType.Episode };
        var anidbEp2 = new AniDB_Episode { EpisodeID = 2, EpisodeNumber = 2, EpisodeType = EpisodeType.Episode };
        var anilistEp1 = new Anilist_Episode(AnimeId, 1);
        var anilistEp2 = new Anilist_Episode(AnimeId, 2);
        return (
            new() { [1] = anidbEp1, [2] = anidbEp2 },
            new() { [anilistEp1.AnilistEpisodeID] = anilistEp1, [anilistEp2.AnilistEpisodeID] = anilistEp2 }
        );
    }

    // Same shape as the TMDB test: a coincidental schedule date pulled AniDB episode 2 onto AniList
    // episode 1, leaving episode 1 to fall back to the next positional candidate. Weak matches swap
    // back into AniDB order, and the rating travels with the pairing.
    [Fact]
    public void ReconcileEpisodeOrderInversions_SwapsBackToAnidbOrder_WhenBothMatchesAreWeak()
    {
        var (anidb, anilist) = TwoEpisodes();
        var anilistEp1 = anilist[AnilistUtility.PackEpisodeID(AnimeId, 1)];
        var anilistEp2 = anilist[AnilistUtility.PackEpisodeID(AnimeId, 2)];

        var xrefEp1 = new CrossRef_AniDB_Anilist_Episode(0, 1, AnimeId, anilistEp2.AnilistEpisodeID, 2, MatchRating.FirstAvailable);
        var xrefEp2 = new CrossRef_AniDB_Anilist_Episode(0, 2, AnimeId, anilistEp1.AnilistEpisodeID, 1, MatchRating.DateMatches);

        AnilistLinkingService.ReconcileEpisodeOrderInversions(anidb, anilist, [xrefEp1, xrefEp2]);

        Assert.Equal(anilistEp1.AnilistEpisodeID, xrefEp1.AnilistEpisodeID);
        Assert.Equal(1, xrefEp1.EpisodeNumber);
        Assert.Equal(MatchRating.DateMatches, xrefEp1.MatchRating);
        Assert.Equal(anilistEp2.AnilistEpisodeID, xrefEp2.AnilistEpisodeID);
        Assert.Equal(2, xrefEp2.EpisodeNumber);
        Assert.Equal(MatchRating.FirstAvailable, xrefEp2.MatchRating);
    }

    [Fact]
    public void ReconcileEpisodeOrderInversions_LeavesOrderAlone_WhenAlreadyCorrect()
    {
        var (anidb, anilist) = TwoEpisodes();
        var anilistEp1 = anilist[AnilistUtility.PackEpisodeID(AnimeId, 1)];
        var anilistEp2 = anilist[AnilistUtility.PackEpisodeID(AnimeId, 2)];

        var xrefEp1 = new CrossRef_AniDB_Anilist_Episode(0, 1, AnimeId, anilistEp1.AnilistEpisodeID, 1, MatchRating.DateMatches);
        var xrefEp2 = new CrossRef_AniDB_Anilist_Episode(0, 2, AnimeId, anilistEp2.AnilistEpisodeID, 2, MatchRating.FirstAvailable);

        AnilistLinkingService.ReconcileEpisodeOrderInversions(anidb, anilist, [xrefEp1, xrefEp2]);

        Assert.Equal(anilistEp1.AnilistEpisodeID, xrefEp1.AnilistEpisodeID);
        Assert.Equal(anilistEp2.AnilistEpisodeID, xrefEp2.AnilistEpisodeID);
    }

    [Theory]
    [InlineData(MatchRating.UserVerified)]
    [InlineData(MatchRating.DateAndNumberMatches)]
    [InlineData(MatchRating.DateOffsetMatches)]
    public void ReconcileEpisodeOrderInversions_DoesNotTouchStrongMatches(MatchRating strongRating)
    {
        var (anidb, anilist) = TwoEpisodes();
        var anilistEp1 = anilist[AnilistUtility.PackEpisodeID(AnimeId, 1)];
        var anilistEp2 = anilist[AnilistUtility.PackEpisodeID(AnimeId, 2)];

        // Inverted on purpose, but one side is a strong match, so it must stay put.
        var xrefEp1 = new CrossRef_AniDB_Anilist_Episode(0, 1, AnimeId, anilistEp2.AnilistEpisodeID, 2, strongRating);
        var xrefEp2 = new CrossRef_AniDB_Anilist_Episode(0, 2, AnimeId, anilistEp1.AnilistEpisodeID, 1, MatchRating.FirstAvailable);

        AnilistLinkingService.ReconcileEpisodeOrderInversions(anidb, anilist, [xrefEp1, xrefEp2]);

        Assert.Equal(anilistEp2.AnilistEpisodeID, xrefEp1.AnilistEpisodeID);
        Assert.Equal(anilistEp1.AnilistEpisodeID, xrefEp2.AnilistEpisodeID);
    }

    [Fact]
    public void ReconcileEpisodeOrderInversions_FullyUntangles_ThreeEpisodeReversal()
    {
        var anidb = new Dictionary<int, AniDB_Episode>();
        var anilist = new Dictionary<int, Anilist_Episode>();
        var xrefs = new List<CrossRef_AniDB_Anilist_Episode>();
        for (var number = 1; number <= 3; number++)
        {
            anidb[number] = new AniDB_Episode { EpisodeID = number, EpisodeNumber = number, EpisodeType = EpisodeType.Episode };
            var episode = new Anilist_Episode(AnimeId, number);
            anilist[episode.AnilistEpisodeID] = episode;
        }
        // 1→3, 2→2, 3→1
        for (var number = 1; number <= 3; number++)
        {
            var reversed = 4 - number;
            xrefs.Add(new(0, number, AnimeId, AnilistUtility.PackEpisodeID(AnimeId, reversed), reversed, MatchRating.DateKindaMatches));
        }

        AnilistLinkingService.ReconcileEpisodeOrderInversions(anidb, anilist, xrefs);

        for (var number = 1; number <= 3; number++)
            Assert.Equal(AnilistUtility.PackEpisodeID(AnimeId, number), xrefs[number - 1].AnilistEpisodeID);
    }

    [Fact]
    public void ReconcileEpisodeOrderInversions_DoesNotCrossEpisodeTypeBoundary()
    {
        var anidbEp = new AniDB_Episode { EpisodeID = 1, EpisodeNumber = 1, EpisodeType = EpisodeType.Episode };
        var anidbSpecial = new AniDB_Episode { EpisodeID = 2, EpisodeNumber = 1, EpisodeType = EpisodeType.Special };
        var anidb = new Dictionary<int, AniDB_Episode> { [1] = anidbEp, [2] = anidbSpecial };
        var anilistEp1 = new Anilist_Episode(AnimeId, 1);
        var anilistEp2 = new Anilist_Episode(AnimeId, 2);
        var anilist = new Dictionary<int, Anilist_Episode> { [anilistEp1.AnilistEpisodeID] = anilistEp1, [anilistEp2.AnilistEpisodeID] = anilistEp2 };

        var xrefEp = new CrossRef_AniDB_Anilist_Episode(0, 1, AnimeId, anilistEp2.AnilistEpisodeID, 2, MatchRating.FirstAvailable);
        var xrefSpecial = new CrossRef_AniDB_Anilist_Episode(0, 2, AnimeId, anilistEp1.AnilistEpisodeID, 1, MatchRating.DateMatches);

        AnilistLinkingService.ReconcileEpisodeOrderInversions(anidb, anilist, [xrefEp, xrefSpecial]);

        Assert.Equal(anilistEp2.AnilistEpisodeID, xrefEp.AnilistEpisodeID);
        Assert.Equal(anilistEp1.AnilistEpisodeID, xrefSpecial.AnilistEpisodeID);
    }
}
