using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Release;

#nullable enable
namespace Shoko.Server.API.v3.Models.Release;

public class ReleaseCrossReference : IReleaseVideoCrossReference
{
    /// <summary>
    /// AniDB episode ID.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int AnidbEpisodeID { get; init; }

    /// <summary>
    /// AniDB anime ID, if known by the provider. Otherwise we'll fetch it
    /// later using the <see cref="AnidbEpisodeID"/>.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? AnidbAnimeID { get; init; }

    /// <summary>
    /// Where in the <see cref="AnidbEpisodeID"/> the video starts covering in 
    /// the range [0, 99], but must be less than <see cref="PercentageEnd"/>.
    /// </summary>
    [Required]
    [Range(0, 99)]
    public int PercentageStart { get; init; }

    /// <summary>
    /// Where in the <see cref="AnidbEpisodeID"/> the video stops covering in
    /// the range [1, 100], but must be greater than <see cref="PercentageStart"/>.
    /// </summary>
    [Required]
    [Range(1, 100)]
    public int PercentageEnd { get; init; }

    public ReleaseCrossReference() { }

    public ReleaseCrossReference(IReleaseVideoCrossReference crossReference)
    {
        AnidbEpisodeID = crossReference.AnidbEpisodeID;
        AnidbAnimeID = crossReference.AnidbAnimeID;
        PercentageStart = crossReference.PercentageStart;
        PercentageEnd = crossReference.PercentageEnd;
    }
}
