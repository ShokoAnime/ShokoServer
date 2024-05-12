
using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
/// File/Episode cross-references.
/// </summary>
public class FileCrossReference
{
    /// <summary>
    /// The Series IDs.
    /// </summary>
    public SeriesCrossReferenceIDs SeriesID { get; set; }

    /// <summary>
    /// The Episode IDs.
    /// </summary>
    public List<EpisodeCrossReferenceIDs> EpisodeIDs { get; set; }

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
        /// Any TvDB IDs linked to the AniDB episode.
        /// </summary>
        public List<int> TvDB { get; set; }

        /// <summary>
        /// ED2K hash to look up the file by hash + file size.
        /// </summary>
        public string ED2K { get; set; }

        /// <summary>
        /// File size to look up the file by hash + file size.
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Percentage file is matched to the episode.
        /// </summary>
        public Tuple<int, int> Percentage { get; set; }

        /// <summary>
        /// The cross-reference source.
        /// </summary>
        public string Source { get; set; }
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
        /// Any TvDB IDs linked to the AniDB series.
        /// </summary>
        public List<int> TvDB { get; set; }
    }

    public static List<FileCrossReference> From(IEnumerable<SVR_CrossRef_File_Episode> crossReferences)
        => crossReferences
                .Select(xref =>
                {
                    // Percentages.
                    Tuple<int, int> percentage = new(0, 100);
                    if (xref.Percentage < 100)
                    {
                        var releaseGroup = xref.CrossRefSource == (int)CrossRefSource.AniDB ? RepoFactory.AniDB_File.GetByHashAndFileSize(xref.Hash, xref.FileSize)?.GroupID ?? -1 : 0;
                        var xrefs = RepoFactory.CrossRef_File_Episode.GetByEpisodeID(xref.EpisodeID)
                            // Filter to only cross-references which are partially linked to the episode and from the same group as the current cross-reference.
                            .Where(xref2 => xref2.Percentage < 100 && (xref2.CrossRefSource == (int)CrossRefSource.AniDB ? RepoFactory.AniDB_File.GetByHashAndFileSize(xref2.Hash, xref2.FileSize)?.GroupID ?? -1 : 0) == releaseGroup)
                            // Order by episode order because it may be returned in semi-random order from the cache, and we need them in the order they arrived in from remote.
                            .OrderBy(xref => xref.EpisodeOrder)
                            .ToList();
                        var index = xrefs.FindIndex(xref2 => xref2.CrossRef_File_EpisodeID == xref.CrossRef_File_EpisodeID);
                        if (index > 0)
                        {
                            // Note: this is bound to be inaccurate if we don't have all the files linked to the episode locally, but as long
                            // as we have all the needed files/cross-references then it will work 100% of the time (pun intended).
                            var accumulatedPercentage = xrefs[..index].Sum(xref => xref.Percentage);
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
                            throw new IndexOutOfRangeException($"Unable to find cross-reference for hash on the episode. (Hash={xref.Hash},Episode={xref.EpisodeID},Anime={xref.AnimeID})");
                        }
                    }

                    var shokoEpisode = xref.GetAnimeEpisode();
                    return new EpisodeCrossReferenceIDs
                    {
                        ID = shokoEpisode?.AnimeEpisodeID,
                        AniDB = xref.EpisodeID,
                        TvDB = shokoEpisode?.TvDBEpisodes.Select(b => b.Id).ToList() ?? [],
                        Percentage = percentage,
                        Source = xref.CrossRefSource == (int)CrossRefSource.AniDB ? "AniDB" : "User",
                    };
                })
                .OrderBy(xref => xref.Percentage.Item1)
                .ThenByDescending(xref => xref.Percentage.Item2)
                .GroupBy(xref => xref.AniDB)
                .Select(tuples =>
                {
                    var shokoSeries = RepoFactory.AnimeSeries.GetByID(tuples.Key);
                    return new FileCrossReference
                    {
                        SeriesID = new SeriesCrossReferenceIDs
                        {
                            ID = shokoSeries?.AnimeSeriesID,
                            AniDB = tuples.Key,
                            TvDB = shokoSeries?.GetTvDBSeries().Select(b => b.SeriesID).ToList() ?? [],
                        },
                        EpisodeIDs = [.. tuples],
                    };
                })
                .ToList();
}
