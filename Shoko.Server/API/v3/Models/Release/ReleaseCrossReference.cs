#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Video.Release;

namespace Shoko.Server.API.v3.Models.Release;

public class ReleaseCrossReference : IReleaseVideoCrossReference
{
    /// <summary>AniDB episode ID.</summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int AnidbEpisodeID { get; init; }

    /// <summary>AniDB anime ID, if known.</summary>
    [Range(1, int.MaxValue)]
    public int? AnidbAnimeID { get; init; }

    /// <summary>Where the video starts covering, in [0, 99].</summary>
    [Required]
    [Range(0, 99)]
    public int PercentageStart { get; init; }

    /// <summary>Where the video stops covering, in [1, 100].</summary>
    [Required]
    [Range(1, 100)]
    public int PercentageEnd { get; init; }

    public ReleaseCrossReference() { }

    public ReleaseCrossReference(IReleaseVideoCrossReference crossReference)
    {
        AnidbEpisodeID = crossReference.GetAnidbEpisodeID() ?? 0;
        AnidbAnimeID = crossReference.GetAnidbAnimeID();
        PercentageStart = crossReference.PercentageStart;
        PercentageEnd = crossReference.PercentageEnd;
    }

    #region IReleaseVideoCrossReference Implementation

    IReadOnlyDictionary<string, string> IReleaseVideoCrossReference.ProviderIDs
    {
        get
        {
            var dict = new Dictionary<string, string>
            {
                [CrossReferenceIDs.AniDB_Episode] = AnidbEpisodeID.ToString(),
            };
            if (AnidbAnimeID.HasValue)
                dict[CrossReferenceIDs.AniDB_Anime] = AnidbAnimeID.Value.ToString();
            return dict;
        }
    }

    #endregion
}