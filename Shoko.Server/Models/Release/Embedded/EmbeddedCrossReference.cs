using Shoko.Abstractions.Release;

#nullable enable
namespace Shoko.Server.Models.Release;

public class EmbeddedCrossReference : IReleaseVideoCrossReference
{
    public int AnidbEpisodeID { get; set; }
    public int? AnidbAnimeID { get; set; }
    public int PercentageStart { get; set; }
    public int PercentageEnd { get; set; }

    public EmbeddedCrossReference() { }

    public EmbeddedCrossReference(IReleaseVideoCrossReference crossReference)
    {
        AnidbEpisodeID = crossReference.AnidbEpisodeID;
        AnidbAnimeID = crossReference.AnidbAnimeID;
        PercentageStart = crossReference.PercentageStart;
        PercentageEnd = crossReference.PercentageEnd;
    }
}
