
using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
/// File/Episode cross-references.
/// </summary>
public class FileCrossReference
{
    /// <summary>
    /// The Series IDs.
    /// </summary>
    public SeriesCrossReferenceIDs SeriesID { get; set; } = new();

    /// <summary>
    /// The Episode IDs.
    /// </summary>
    public List<EpisodeCrossReferenceIDs> EpisodeIDs { get; set; } = [];

    /// <summary>
    /// File episode cross-reference for a series.
    /// </summary>
    public class EpisodeCrossReferenceIDs
    {
        /// <summary>
        /// The Shoko ID, if the local metadata has been created yet.
        /// /// </summary>
        public int? ID { get; set; }

        /// <summary>
        /// The AniDB ID.
        /// </summary>
        public int AniDB { get; set; }

        /// <summary>
        /// The Movie DataBase (TMDB) Cross-Reference IDs.
        /// </summary>
        public Episode.EpisodeIDs.TmdbEpisodeIDs TMDB { get; set; } = new();

        /// <summary>
        /// The AniDB Release Group's ID, or null if this is a manually linked
        /// file. May also be 0 if the release group is currently unknown.
        /// </summary>
        public int? ReleaseGroup { get; set; }

        /// <summary>
        /// ED2K hash to look up the file by hash + file size.
        /// </summary>
        public string ED2K { get; set; } = string.Empty;

        /// <summary>
        /// File size to look up the file by hash + file size.
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Percentage file is matched to the episode.
        /// </summary>
        public CrossReferencePercentage Percentage { get; set; } = new();

        /// <summary>
        /// The cross-reference source.
        /// </summary>
        public string Source { get; set; } = string.Empty;
    }

    public class CrossReferencePercentage
    {
        /// <summary>
        /// File/episode cross-reference percentage range end.
        /// </summary>
        public int Start { get; set; }

        /// <summary>
        /// File/episode cross-reference percentage range end.
        /// </summary>
        public int End { get; set; }

        /// <summary>
        /// The raw percentage to "group" the cross-references by.
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// The assumed number of groups in the release, to group the
        /// cross-references by.
        /// </summary>
        public int Group { get; set; }
    }

    /// <summary>
    /// File series cross-reference.
    /// </summary>
    public class SeriesCrossReferenceIDs
    {
        /// <summary>
        /// The Shoko ID, if the local metadata has been created yet.
        /// /// </summary>
        public int? ID { get; set; }

        /// <summary>
        /// The AniDB ID.
        /// </summary>
        public int AniDB { get; set; }

        /// <summary>
        /// The Movie DataBase (TMDB) Cross-Reference IDs.
        /// </summary>
        public Series.SeriesIDs.TmdbSeriesIDs TMDB { get; set; } = new();
    }

    private static int PercentageToFileCount(int percentage)
        => percentage switch
        {
            100 => 1,
            99 => 1,
            51 => 2,
            50 => 2,
            49 => 2,
            34 => 3,
            33 => 3,
            32 => 3,
            26 => 4,
            25 => 4,
            24 => 4,
            21 => 5,
            20 => 5,
            19 => 5,
            17 => 6,
            16 => 6,
            15 => 7,
            14 => 7,
            13 => 8,
            12 => 8,
            // 11 => 9, // 9 also overlaps with 12%, so we skip this for now.
            10 => 10,
            _ => 0, // anything below this we can't reliably measure.
        };

    public static List<FileCrossReference> From(IEnumerable<IVideoCrossReference> crossReferences)
        => crossReferences
                .Where(xref => xref.Video is not null)
                .Select(xref =>
                {
                    // Percentages.
                    Tuple<int, int> percentage = new(0, 100);
                    int? releaseGroup = xref.Source == DataSourceEnum.AniDB ? RepoFactory.AniDB_File.GetByEd2kAndFileSize(xref.ED2K, xref.Size)?.GroupID ?? 0 : null;
                    var assumedFileCount = PercentageToFileCount(xref.Percentage);
                    if (assumedFileCount > 1)
                    {
                        var xrefs = RepoFactory.CrossRef_File_Episode.GetByEpisodeID(xref.AnidbEpisodeID)
                            // Filter to only cross-references which are partially linked in the same number of parts to the episode, and from the same group as the current cross-reference.
                            .Where(xref2 => PercentageToFileCount(xref2.Percentage) == assumedFileCount && (xref2.CrossRefSource == (int)CrossRefSource.AniDB ? RepoFactory.AniDB_File.GetByEd2kAndFileSize(xref2.Hash, xref2.FileSize)?.GroupID ?? -1 : null) == releaseGroup)
                            // This will order by the "full" episode if the xref is linked to both a "full" episode and "part" episode,
                            // then fall back on the episode order if either a "full" episode is not available, or if it's for cross-references
                            // for a single-file-multiple-episodes file.
                            .Select(xref2 => (
                                xref: xref2,
                                episode: RepoFactory.CrossRef_File_Episode.GetByEd2k(xref2.Hash)
                                    .FirstOrDefault(xref3 => xref3.Percentage == 100 && (xref3.CrossRefSource == (int)CrossRefSource.AniDB ? RepoFactory.AniDB_File.GetByEd2kAndFileSize(xref3.Hash, xref3.FileSize)?.GroupID ?? -1 : null) == releaseGroup)
                                    ?.AniDBEpisode
                            ))
                            .OrderBy(tuple => tuple.episode?.EpisodeTypeEnum)
                            .ThenBy(tuple => tuple.episode?.EpisodeNumber)
                            .ThenBy(tuple => tuple.xref.EpisodeOrder)
                            .ToList();
                        var index = xrefs.FindIndex(tuple => string.Equals(tuple.xref.Hash, xref.ED2K) && tuple.xref.FileSize == xref.Size);
                        if (index > 0)
                        {
                            // Note: this is bound to be inaccurate if we don't have all the files linked to the episode locally, but as long
                            // as we have all the needed files/cross-references then it will work 100% of the time (pun intended).
                            var accumulatedPercentage = xrefs[..index].Sum(tuple => tuple.xref.Percentage);
                            var totalPercentage = accumulatedPercentage + xref.Percentage;
                            if (totalPercentage >= 95)
                                totalPercentage = 100;
                            percentage = new(accumulatedPercentage, totalPercentage);
                        }
                        else if (index == 0)
                        {
                            percentage = new(0, xref.Percentage);
                        }
                        else
                        {
                            percentage = new(0, 0);
                        }
                    }

                    var shokoEpisode = xref.ShokoEpisode as SVR_AnimeEpisode;
                    return (
                        xref,
                        dto: new EpisodeCrossReferenceIDs
                        {
                            ID = shokoEpisode?.AnimeEpisodeID,
                            AniDB = xref.AnidbEpisodeID,
                            ReleaseGroup = releaseGroup,
                            TMDB = new()
                            {
                                Episode = shokoEpisode?.TmdbEpisodeCrossReferences
                                    .Where(xref => xref.TmdbEpisodeID != 0)
                                    .Select(xref => xref.TmdbEpisodeID)
                                    .ToList() ?? [],
                                Movie = shokoEpisode?.TmdbMovieCrossReferences
                                    .Select(xref => xref.TmdbMovieID)
                                    .ToList() ?? [],
                                Show = shokoEpisode?.TmdbEpisodeCrossReferences
                                    .Where(xref => xref.TmdbShowID != 0)
                                    .Select(xref => xref.TmdbShowID)
                                    .Distinct()
                                    .ToList() ?? [],
                            },
                            Percentage = new()
                            {
                                Size = xref.Percentage,
                                Group = assumedFileCount,
                                Start = percentage.Item1,
                                End = percentage.Item2,
                            },
                            ED2K = xref.ED2K,
                            FileSize = xref.Size,
                            Source = xref.Source == DataSourceEnum.AniDB ? "AniDB" : "User",
                        }
                    );
                })
                .OrderBy(tuple => tuple.dto.Percentage.Start)
                .ThenByDescending(tuple => tuple.dto.Percentage.End)
                // Temp solution because xref.AnimeID cannot be fully trusted because the
                // episode may belong to a different anime. Until this is resolved then
                // we will attempt to lookup the episode to grab it's id but fallback
                // to the cross-reference anime id if the episode is not locally available
                // yet.
                .GroupBy(tuple => tuple.xref.AnidbEpisode?.SeriesID ?? tuple.xref.AnidbAnimeID)
                .Select(tuples =>
                {
                    var shokoSeries = RepoFactory.AnimeSeries.GetByAnimeID(tuples.Key);
                    return new FileCrossReference
                    {
                        SeriesID = new SeriesCrossReferenceIDs
                        {
                            ID = shokoSeries?.AnimeSeriesID,
                            AniDB = tuples.Key,
                            TMDB = new()
                            {
                                Movie = shokoSeries?.TmdbMovieCrossReferences.Select(xref => xref.TmdbMovieID).Distinct().ToList() ?? [],
                                Show = shokoSeries?.TmdbShowCrossReferences.Select(xref => xref.TmdbShowID).Distinct().ToList() ?? [],
                            },
                        },
                        EpisodeIDs = tuples.Select(tuple => tuple.dto).ToList(),
                    };
                })
                .ToList();
}
