using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video.Release;

namespace Shoko.Server.API.v3.Models.Release;

public class ReleaseCrossReference : IReleaseVideoCrossReference
{
    /// <summary>
    /// AniDB episode ID, if the cross-reference links an AniDB episode. Also
    /// found in <see cref="IDs"/>; set either.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? AnidbEpisodeID { get; init; }

    /// <summary>
    /// AniDB anime ID, if known by the provider. Otherwise we'll fetch it
    /// later using the <see cref="AnidbEpisodeID"/>. Also found in
    /// <see cref="IDs"/>; set either.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? AnidbAnimeID { get; init; }

    /// <summary>
    /// Where in the linked entries the video starts covering in
    /// the range [0, 99], but must be less than <see cref="PercentageEnd"/>.
    /// </summary>
    [Required]
    [Range(0, 99)]
    public int PercentageStart { get; init; }

    /// <summary>
    /// Where in the linked entries the video stops covering in
    /// the range [1, 100], but must be greater than <see cref="PercentageStart"/>.
    /// </summary>
    [Required]
    [Range(1, 100)]
    public int PercentageEnd { get; init; }

    /// <summary>
    /// The entries the video is linked to, from any source, as metadata IDs,
    /// e.g. <c>anidb://episode/1</c>. The AniDB IDs above are included when
    /// read, and added when written.
    /// </summary>
    public List<MetadataGuid>? IDs { get; init; }

    public ReleaseCrossReference() { }

    public ReleaseCrossReference(IReleaseVideoCrossReference crossReference)
    {
        AnidbEpisodeID = crossReference.AnidbEpisodeID is > 0 and var episodeID ? episodeID : null;
        AnidbAnimeID = crossReference.AnidbAnimeID;
        PercentageStart = crossReference.PercentageStart;
        PercentageEnd = crossReference.PercentageEnd;
        IDs = [.. crossReference.IDs];
    }

    #region IReleaseVideoCrossReference Implementation

    IReadOnlyList<MetadataGuid> IReleaseVideoCrossReference.IDs
    {
        get
        {
            var xref = new ReleaseVideoCrossReference { IDs = [.. IDs ?? []] };
            if (AnidbEpisodeID is > 0 && xref.AnidbEpisodeID != AnidbEpisodeID)
                xref.AnidbEpisodeID = AnidbEpisodeID.Value;
            if (AnidbAnimeID is > 0 && xref.AnidbAnimeID != AnidbAnimeID)
                xref.AnidbAnimeID = AnidbAnimeID;
            return xref.IDs;
        }
    }

    #endregion
}
