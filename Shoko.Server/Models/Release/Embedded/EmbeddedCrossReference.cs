using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Release;

#nullable enable
namespace Shoko.Server.Models.Release;

public class EmbeddedCrossReference : IReleaseVideoCrossReference
{
    public int AnidbEpisodeID { get; set; }
    public int? AnidbAnimeID { get; set; }
    public int PercentageStart { get; set; }
    public int PercentageEnd { get; set; }

    /// <summary>
    /// Episode type (Episode, Special, Credits, etc.).
    /// </summary>
    public EpisodeType EpisodeType { get; set; } = EpisodeType.Episode;

    /// <summary>
    /// Episode number within its type (e.g. 1 for episode 1, 2 for special 2).
    /// </summary>
    public int EpisodeNumber { get; set; }

    public EmbeddedCrossReference() { }

    public EmbeddedCrossReference(IReleaseVideoCrossReference crossReference)
    {
        AnidbEpisodeID = crossReference.AnidbEpisodeID;
        AnidbAnimeID = crossReference.AnidbAnimeID;
        PercentageStart = crossReference.PercentageStart;
        PercentageEnd = crossReference.PercentageEnd;
        if (crossReference is EmbeddedCrossReference embedded)
        {
            EpisodeType = embedded.EpisodeType;
            EpisodeNumber = embedded.EpisodeNumber;
        }
    }

    /// <summary>
    /// Returns the episode identifier in the standard Shoko format, e.g.
    /// <c>1</c> for episode 1, <c>S2</c> for special 2, <c>C1</c> for credits 1.
    /// </summary>
    public string ToEpisodeString()
    {
        var prefix = EpisodeType switch
        {
            EpisodeType.Special => "S",
            EpisodeType.Credits => "C",
            EpisodeType.Trailer => "T",
            EpisodeType.Parody => "P",
            EpisodeType.Other => "O",
            _ => string.Empty,
        };
        return prefix + EpisodeNumber;
    }
}
