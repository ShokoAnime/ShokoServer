
using System.ComponentModel.DataAnnotations;

namespace Shoko.Plugin.Abstractions.Release;

/// <summary>
/// Video cross-reference included in an <see cref="IReleaseInfo"/>.
/// </summary>
public class ReleaseVideoCrossReference : IReleaseVideoCrossReference
{
    /// <inheritdoc />
    public int AnidbEpisodeID { get; set; }

    /// <inheritdoc />
    public int? AnidbAnimeID { get; set; }

    /// <summary>
    /// Where in the <see cref="AnidbEpisodeID"/> the video starts covering.
    /// If null, then the video starts at the beginning of the episode.
    /// Can be in the range [0, 99], but must be less than the
    /// <see cref="PercentageEnd"/> if both are set.
    /// </summary>
    [Range(0, 99)]
    public int? PercentageStart { get; set; }

    /// <summary>
    /// Where in the <see cref="AnidbEpisodeID"/> the video stops covering.
    /// If null, then the video stops at the end of the episode.
    /// Can be in the range [1, 100], but must be greater than the
    /// <see cref="PercentageStart"/> if both are set.
    /// </summary>
    [Range(1, 100)]
    public int? PercentageEnd { get; set; }

    /// <summary>
    /// Constructs a new <see cref="ReleaseVideoCrossReference"/> instance.
    /// </summary>
    public ReleaseVideoCrossReference() { }

    /// <summary>
    /// Constructs a new <see cref="ReleaseVideoCrossReference"/> instance from
    /// a <see cref="IReleaseVideoCrossReference"/>.
    /// </summary>
    /// <param name="reference">The <see cref="IReleaseVideoCrossReference"/> to
    /// construct from.</param>
    public ReleaseVideoCrossReference(IReleaseVideoCrossReference reference)
    {
        AnidbEpisodeID = reference.AnidbEpisodeID;
        AnidbAnimeID = reference.AnidbAnimeID;
        // Check if it's a ReleaseVideoCrossReference to preserve the nullable state.
        if (reference is ReleaseVideoCrossReference vRef)
        {
            PercentageStart = vRef.PercentageStart;
            PercentageEnd = vRef.PercentageEnd;
        }
        else
        {
            PercentageStart = reference.PercentageStart;
            PercentageEnd = reference.PercentageEnd;
        }
    }

    #region IReleaseVideoCrossReference Implementation

    /// <inheritdoc />
    int IReleaseVideoCrossReference.PercentageStart => PercentageStart ?? 0;

    /// <inheritdoc />
    int IReleaseVideoCrossReference.PercentageEnd => PercentageEnd ?? 100;

    #endregion
}
