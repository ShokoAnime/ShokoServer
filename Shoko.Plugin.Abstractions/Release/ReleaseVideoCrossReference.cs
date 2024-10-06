
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

    /// <inheritdoc />
    public int Order { get; set; }

    /// <summary>
    /// Where in the <see cref="AnidbEpisodeID"/> the video starts covering.
    /// If null, then the video starts at the beginning of the episode.
    /// Can be in the range [0, 100], but must be less than the
    /// <see cref="PercentageEnd"/> if both are set.
    /// </summary>
    public int? PercentageStart { get; set; }

    /// <summary>
    /// Where in the <see cref="AnidbEpisodeID"/> the video stops covering.
    /// If null, then the video stops at the end of the episode.
    /// Can be in the range [0, 100], but must be greater than the
    /// <see cref="PercentageStart"/> if both are set.
    /// </summary>
    public int? PercentageEnd { get; set; }

    /// <inheritdoc />
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
        Order = reference.Order;
        PercentageStart = reference.PercentageStart;
        PercentageEnd = reference.PercentageEnd;
    }
    
    #region IReleaseVideoCrossReference Implementation

    /// <inheritdoc />
    int IReleaseVideoCrossReference.PercentageStart => PercentageStart ?? 0;

    /// <inheritdoc />
    int IReleaseVideoCrossReference.PercentageEnd => PercentageEnd ?? 100;

    #endregion
}
