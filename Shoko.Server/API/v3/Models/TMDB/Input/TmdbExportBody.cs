using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.CrossReference;
using static Shoko.Server.API.v3.Controllers.TmdbController;

#nullable enable
namespace Shoko.Server.API.v3.Models.TMDB.Input;

public class TmdbExportBody
{
    /// <summary>
    /// Include only cross-references with the given AniDB episode(s).
    /// </summary>
    public HashSet<int>? AnidbEpisodeIDs { get; set; } = null;

    /// <summary>
    /// Include only cross-references with the given AniDB anime(s).
    /// </summary>
    public HashSet<int>? AnidbAnimeIDs { get; set; } = null;

    /// <summary>
    /// Include only cross-references with the given TMDB show(s).
    /// </summary>
    public HashSet<int>? TmdbShowIDs { get; set; } = null;

    /// <summary>
    /// Include only cross-references with the given TMDB episode(s).
    /// </summary>
    public HashSet<int>? TmdbEpisodeIDs { get; set; } = null;

    /// <summary>
    /// Include only cross-references with the given TMDB movie(s).
    /// </summary>
    public HashSet<int>? TmdbMovieIDs { get; set; } = null;

    /// <summary>
    /// Include/exclude automatically made cross-references.
    /// </summary>
    [DefaultValue(IncludeOnlyFilter.True)]
    public IncludeOnlyFilter Automatic { get; set; } = IncludeOnlyFilter.True;

    /// <summary>
    /// Include/exclude cross-references with an episode. That is movie cross-references with an anidb episode set, or episode cross-references with a tmdb episode set.
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
    public HashSet<CrossReferenceExportType>? SectionSet { get; set; } = null;

    /// <summary>
    /// Determines whether the movie filter is enabled.
    /// </summary>
    [JsonIgnore]
    public bool MovieFilerEnabled => AnidbAnimeIDs is not null || AnidbEpisodeIDs is not null || TmdbMovieIDs is not null;

    /// <summary>
    /// Determines whether the given movie cross-reference should be included in the export.
    /// </summary>
    /// <param name="xref">The movie cross-reference to check.</param>
    /// <returns><c>true</c> if the cross-reference should be included, <c>false</c> otherwise.</returns>
    public bool ShouldKeep(CrossRef_AniDB_TMDB_Movie xref)
    {
        if (!MovieFilerEnabled)
            return true;
        if (AnidbAnimeIDs is not null && AnidbAnimeIDs.Contains(xref.AnidbAnimeID))
            return true;
        if (AnidbEpisodeIDs is not null && AnidbEpisodeIDs.Contains(xref.AnidbEpisodeID))
            return true;
        if (TmdbMovieIDs is not null && TmdbMovieIDs.Contains(xref.TmdbMovieID))
            return true;
        return false;
    }

    /// <summary>
    /// Determines whether episode filtering is enabled.
    /// </summary>
    [JsonIgnore]
    public bool EpisodeFilterEnabled => AnidbAnimeIDs is not null || AnidbEpisodeIDs is not null || TmdbShowIDs is not null || TmdbEpisodeIDs is not null;

    /// <summary>
    /// Determines whether the given episode cross-reference should be included in the export.
    /// </summary>
    /// <param name="xref">The episode cross-reference to check.</param>
    /// <returns><c>true</c> if the cross-reference should be included, <c>false</c> otherwise.</returns>
    public bool ShouldKeep(CrossRef_AniDB_TMDB_Episode xref)
    {
        if (!EpisodeFilterEnabled)
            return true;
        if (AnidbAnimeIDs is not null && AnidbAnimeIDs.Contains(xref.AnidbAnimeID))
            return true;
        if (AnidbEpisodeIDs is not null && AnidbEpisodeIDs.Contains(xref.AnidbEpisodeID))
            return true;
        if (TmdbShowIDs is not null && TmdbShowIDs.Contains(xref.TmdbShowID))
            return true;
        if (TmdbEpisodeIDs is not null && TmdbEpisodeIDs.Contains(xref.TmdbEpisodeID))
            return true;
        return false;
    }
}
