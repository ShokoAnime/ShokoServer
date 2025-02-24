using System;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Server.Extensions;

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

    public static EmbeddedCrossReference? FromString(string str)
    {
        var (anidbEpisodeID, anidbAnimeID, percentageStart, percentageEnd) = str.Split('.', StringSplitOptions.TrimEntries);
        if (string.IsNullOrEmpty(anidbEpisodeID) || string.IsNullOrEmpty(percentageStart) || string.IsNullOrEmpty(percentageEnd))
            return null;

        return new()
        {
            AnidbEpisodeID = int.Parse(anidbEpisodeID),
            AnidbAnimeID = string.IsNullOrEmpty(anidbAnimeID) ? null : int.Parse(anidbAnimeID),
            PercentageStart = int.Parse(percentageStart),
            PercentageEnd = int.Parse(percentageEnd)
        };
    }
}

public static class IReleaseVideoCrossReferenceExtensions
{
    public static string ToEmbeddedString(this IReleaseVideoCrossReference reference)
        => $"{reference.AnidbEpisodeID}.{reference.AnidbAnimeID?.ToString() ?? string.Empty}.{reference.PercentageStart}.{reference.PercentageEnd}";
}
