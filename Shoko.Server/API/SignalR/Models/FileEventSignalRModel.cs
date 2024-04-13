using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions;
using Shoko.Server.Models;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class FileEventSignalRModel
{
    public FileEventSignalRModel(FileEventArgs eventArgs)
    {
        RelativePath = eventArgs.RelativePath;
        FileID = eventArgs.FileInfo.VideoID;
        FileLocationID = eventArgs.FileInfo.ID;
        ImportFolderID = eventArgs.ImportFolder.ID;
        var episodes = eventArgs.EpisodeInfo
            .Cast<SVR_AniDB_Episode>()
            .Select(e => e.GetShokoEpisode())
            .OfType<SVR_AnimeEpisode>()
            .ToList();
        var seriesToGroupDict = episodes
            .GroupBy(e => e.AnimeSeriesID)
            .Select(e => e.First().GetAnimeSeries())
            .ToDictionary(s => s.AnimeSeriesID, s => s.AnimeGroupID);
        CrossReferences = episodes
            .Select(e => new FileCrossReferenceSignalRModel()
            {
                EpisodeID = e.AnimeEpisodeID,
                SeriesID = e.AnimeSeriesID,
                GroupID = seriesToGroupDict[e.AnimeSeriesID]
            })
            .ToList();
    }

    /// <summary>
    /// Shoko file id.
    /// </summary>
    public int FileID { get; }

    /// <summary>
    /// Shoko file location id.
    /// </summary>
    public int FileLocationID { get; }

    /// <summary>
    /// The ID of the import folder the event was detected in.
    /// </summary>
    public int ImportFolderID { get; }

    /// <summary>
    /// The relative path of the file from the import folder base location.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// Cross references of episodes linked to this file.
    /// </summary>
    public IReadOnlyList<FileCrossReferenceSignalRModel> CrossReferences { get; }
}

public class FileCrossReferenceSignalRModel
{
    /// <summary>
    /// Shoko episode id.
    /// </summary>
    public int EpisodeID { get; set; }

    /// <summary>
    /// Shoko series id.
    /// </summary>
    public int SeriesID { get; set; }

    /// <summary>
    /// Shoko group id.
    /// </summary>
    public int GroupID { get; set; }
}
