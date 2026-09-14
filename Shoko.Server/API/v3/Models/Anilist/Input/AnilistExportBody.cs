using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.CrossReference;

#nullable enable
namespace Shoko.Server.API.v3.Models.Anilist.Input;

/// <summary>
/// Cross-reference export types for Anilist.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
[Flags]
public enum AnilistCrossReferenceExportType
{
    /// <summary>
    /// Include anime cross-references.
    /// </summary>
    Anime = 1,

    /// <summary>
    /// Include episode cross-references.
    /// </summary>
    Episode = 2,
}

public class AnilistExportBody
{
    /// <summary>
    /// Include only cross-references with the given AniDB episode.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? AnidbEpisodeID { get; set; } = null;

    /// <summary>
    /// Include only cross-references with the given AniDB anime.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? AnidbAnimeID { get; set; } = null;

    /// <summary>
    /// Include only cross-references with the given Anilist anime.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? AnilistAnimeID { get; set; } = null;

    /// <summary>
    /// Include only cross-references with the given Anilist episode.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? AnilistEpisodeID { get; set; } = null;

    /// <summary>
    /// Include/exclude automatically made cross-references.
    /// </summary>
    [DefaultValue(IncludeOnlyFilter.True)]
    public IncludeOnlyFilter Automatic { get; set; } = IncludeOnlyFilter.True;

    /// <summary>
    /// Include/exclude cross-references with episodes.
    /// </summary>
    [DefaultValue(IncludeOnlyFilter.True)]
    public IncludeOnlyFilter WithEpisodes { get; set; } = IncludeOnlyFilter.True;

    /// <summary>
    /// Append human friendly comments in the output file. They serve no purpose other than to enlighten the humans reading the file what each cross-reference is for.
    /// </summary>
    public bool IncludeComments { get; set; } = false;

    /// <summary>
    /// Sections to include in the output file, if we have anything to fill in in the selected sections.
    /// </summary>
    public HashSet<AnilistCrossReferenceExportType>? SectionSet { get; set; } = null;

    /// <summary>
    /// Determines whether the anime filter is enabled.
    /// </summary>
    [JsonIgnore]
    public bool AnimeFilerEnabled => AnidbAnimeID is not null || AnilistAnimeID is not null;

    /// <summary>
    /// Determines whether the given anime cross-reference should be included in the export.
    /// </summary>
    /// <param name="xref">The anime cross-reference to check.</param>
    /// <returns><c>true</c> if the cross-reference should be included, <c>false</c> otherwise.</returns>
    public bool ShouldKeep(CrossRef_AniDB_Anilist_Anime xref)
    {
        if (!AnimeFilerEnabled)
            return true;
        if (AnidbAnimeID is not null && AnidbAnimeID != xref.AnidbAnimeID)
            return false;
        if (AnilistAnimeID is not null && AnilistAnimeID != xref.AnilistAnimeID)
            return false;
        return true;
    }

    /// <summary>
    /// Determines whether the episode filter is enabled.
    /// </summary>
    [JsonIgnore]
    public bool EpisodeFilterEnabled => AnidbAnimeID is not null || AnidbEpisodeID is not null || AnilistAnimeID is not null || AnilistEpisodeID is not null;

    /// <summary>
    /// Determines whether the given episode cross-reference should be included in the export.
    /// </summary>
    /// <param name="xref">The episode cross-reference to check.</param>
    /// <returns><c>true</c> if the cross-reference should be included, <c>false</c> otherwise.</returns>
    public bool ShouldKeep(CrossRef_AniDB_Anilist_Episode xref)
    {
        if (!EpisodeFilterEnabled)
            return true;
        if (AnidbAnimeID is not null && AnidbAnimeID != xref.AnidbAnimeID)
            return false;
        if (AnidbEpisodeID is not null && AnidbEpisodeID != xref.AnidbEpisodeID)
            return false;
        if (AnilistAnimeID is not null && AnilistAnimeID != xref.AnilistAnimeID)
            return false;
        if (AnilistEpisodeID is not null && AnilistEpisodeID != xref.AnilistEpisodeID)
            return false;
        return true;
    }
}
