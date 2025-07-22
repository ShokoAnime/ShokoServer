using System;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Server.Models.Release;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.CrossReference;

/// <summary>
/// Stores a reference between the AniDB_File record and an AniDB_Episode
/// anime details and all the episodes
/// </summary>
/// <remarks>
/// We store the Hash and the FileSize, because this is what enables us to make a call the AniDB UDP API using the FILE command
/// We store the AnimeID so we can download all the episodes from HTTP API (ie the HTTP api call will return the
///
/// Note 1 - A file can have one to many episodes, and an episode can have one to many files
/// Note 2 - By storing this information when a user manually associates a file with an episode, we can recover the manual
/// associations even when they move the files around
/// Note 3 - We can use a combination of the FileName/FileSize to determine the Hash for a file, this enables us to handle the
/// moving of files to different locations without needing to re-hash the file again
/// </remarks>
public class CrossRef_File_Episode : IVideoCrossReference
{
    public int CrossRef_File_EpisodeID { get; set; }

    public string Hash { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public int AnimeID { get; set; }

    public int EpisodeID { get; set; }

    public int Percentage { get; set; }

    public int EpisodeOrder { get; set; }

    public VideoLocal? VideoLocal => RepoFactory.VideoLocal.GetByEd2k(Hash);

    public SVR_AniDB_Episode? AniDBEpisode => RepoFactory.AniDB_Episode.GetByEpisodeID(EpisodeID);

    public SVR_AnimeEpisode? AnimeEpisode => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(EpisodeID);

    public SVR_AniDB_Anime? AniDBAnime => AnimeID is 0 ? null : RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);

    public SVR_AnimeSeries? AnimeSeries => AnimeID is 0 ? null : RepoFactory.AnimeSeries.GetByAnimeID(AnimeID);

    public StoredReleaseInfo? ReleaseInfo => RepoFactory.StoredReleaseInfo.GetByEd2kAndFileSize(Hash, FileSize);

    private (int? LastKnownPercentage, (int Start, int End) Range) _percentageRangeCalculated = (null, (0, 0));

    public (int Start, int End) PercentageRange
    {
        get
        {
            if (_percentageRangeCalculated.LastKnownPercentage == Percentage)
                return _percentageRangeCalculated.Range;

            var releaseInfo = (IReleaseInfo?)ReleaseInfo;
            if (releaseInfo is not null && !IsAnidbProvider(releaseInfo.ProviderName) && releaseInfo.CrossReferences.FirstOrDefault(xref => xref.AnidbEpisodeID == EpisodeID) is { } xref)
            {
                _percentageRangeCalculated = (Percentage, (xref.PercentageStart, xref.PercentageEnd));
                return (xref.PercentageStart, xref.PercentageEnd);
            }

            var percentage = (0, 100);
            var releaseGroup = releaseInfo?.Group;
            var assumedFileCount = PercentageToFileCount(Percentage);
            if (assumedFileCount > 1)
            {
                var xrefs = RepoFactory.CrossRef_File_Episode.GetByEpisodeID(EpisodeID)
                    // Filter to only cross-references which are partially linked in the same number of parts to the episode, and from the same group as the current cross-reference.
                    .Where(xref2 => PercentageToFileCount(xref2.Percentage) == assumedFileCount && xref2.ReleaseInfo is IReleaseInfo xref2ReleaseInfo && xref2ReleaseInfo.Group is { } xref2ReleaseGroup && xref2ReleaseGroup.Equals(releaseGroup))
                    // This will order by the "full" episode if the xref is linked to both a "full" episode and "part" episode,
                    // then fall back on the episode order if either a "full" episode is not available, or if it's for cross-references
                    // for a single-file-multiple-episodes file.
                    .Select(xref2 => (
                        xref: xref2,
                        episode: RepoFactory.CrossRef_File_Episode.GetByEd2k(xref2.Hash)
                            .FirstOrDefault(xref3 => xref3.Percentage == 100 && xref3.ReleaseInfo is IReleaseInfo xref3ReleaseInfo && xref3ReleaseInfo.Group is { } xref3ReleaseGroup && xref3ReleaseGroup.Equals(releaseGroup))
                            ?.AniDBEpisode
                    ))
                    .OrderBy(tuple => tuple.episode?.EpisodeTypeEnum)
                    .ThenBy(tuple => tuple.episode?.EpisodeNumber)
                    .ThenBy(tuple => tuple.xref.EpisodeOrder)
                    .ToList();
                var index = xrefs.FindIndex(tuple => string.Equals(tuple.xref.Hash, Hash) && tuple.xref.FileSize == FileSize);
                if (index > 0)
                {
                    // Note: this is bound to be inaccurate if we don't have all the files linked to the episode locally, but as long
                    // as we have all the needed files/cross-references then it will work 100% of the time (pun intended).
                    var accumulatedPercentage = xrefs[..index].Sum(tuple => tuple.xref.Percentage);
                    var totalPercentage = accumulatedPercentage + Percentage;
                    if (totalPercentage >= 95)
                        totalPercentage = 100;
                    percentage = (accumulatedPercentage, totalPercentage);
                }
                else if (index == 0)
                {
                    percentage = (0, Percentage);
                }
                else
                {
                    percentage = (0, 0);
                }
            }

            _percentageRangeCalculated = (Percentage, percentage);
            return percentage;
        }
    }

    private static bool IsAnidbProvider(string providerName) =>
        providerName is "AniDB" ||
        providerName.StartsWith("AniDB+") ||
        providerName.EndsWith("+AniDB") ||
        providerName.Contains("+AniDB+");

    internal static int PercentageToFileCount(int percentage)
        => percentage switch
        {
            100 => 1,
            50 => 2,
            34 => 3,
            33 => 3,
            25 => 4,
            20 => 5,
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

    public override string ToString() =>
        $"CrossRef_File_Episode (Anime={AnimeID},Episode={EpisodeID},Hash={Hash},FileSize={FileSize},EpisodeOrder={EpisodeOrder},Percentage={Percentage})";

    #region IReleaseVideoCrossReference implementation

    int IReleaseVideoCrossReference.AnidbEpisodeID => EpisodeID;

    int? IReleaseVideoCrossReference.AnidbAnimeID => AnimeID is 0 ? AniDBEpisode?.AnimeID : AnimeID;

    int IReleaseVideoCrossReference.PercentageStart => PercentageRange.Start;

    int IReleaseVideoCrossReference.PercentageEnd => PercentageRange.End;

    #endregion

    #region IVideoCrossReference implementation

    string IVideoCrossReference.ED2K => Hash;

    long IVideoCrossReference.Size => FileSize;

    IReleaseInfo IVideoCrossReference.Release => ReleaseInfo
        ?? throw new NullReferenceException($"Could not find IReleaseInfo for IVideoCrossReference for ED2K {Hash} & file size {FileSize}");

    int IVideoCrossReference.AnidbEpisodeID => EpisodeID;

    int IVideoCrossReference.AnidbAnimeID => AnimeID is 0 ? AniDBEpisode?.AnimeID ?? 0 : AnimeID;

    int IVideoCrossReference.Percentage => Percentage;

    IVideo? IVideoCrossReference.Video => VideoLocal;

    IEpisode? IVideoCrossReference.AnidbEpisode => AniDBEpisode;

    ISeries? IVideoCrossReference.AnidbAnime => AniDBAnime;

    IShokoEpisode? IVideoCrossReference.ShokoEpisode => AnimeEpisode;

    IShokoSeries? IVideoCrossReference.ShokoSeries => AnimeSeries;

    #endregion
}
