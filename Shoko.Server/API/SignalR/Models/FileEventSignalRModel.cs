using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions.Events;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class FileEventSignalRModel
{
    public FileEventSignalRModel(FileEventArgs eventArgs)
    {
        RelativePath = eventArgs.RelativePath;
        FileID = eventArgs.File.VideoID;
        FileLocationID = eventArgs.File.ID;
        ImportFolderID = eventArgs.ImportFolder.ID;
        var xrefs = eventArgs.Video.CrossReferences;
        var episodeDict = eventArgs.Episodes
            .WhereNotNull()
            .ToDictionary(e => e.AnidbEpisodeID, e => e);
        var animeToGroupDict = episodeDict.Values
            .DistinctBy(e => e.SeriesID)
            .Select(e => e.Series)
            .WhereNotNull()
            .ToDictionary(s => s.AnidbAnimeID, s => (s.ID, s.ParentGroupID));
        CrossReferences = xrefs
            .Select(xref => new FileCrossReferenceSignalRModel
            {
                EpisodeID = episodeDict.TryGetValue(xref.AnidbEpisodeID, out var shokoEpisode) ? shokoEpisode.ID : null,
                AnidbEpisodeID = xref.AnidbEpisodeID,
                SeriesID = animeToGroupDict.TryGetValue(xref.AnidbAnimeID, out var tuple) ? tuple.ID : null,
                AnidbAnimeID = xref.AnidbAnimeID,
                GroupID = animeToGroupDict.TryGetValue(xref.AnidbAnimeID, out tuple) ? tuple.ParentGroupID : null,
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
    public int? EpisodeID { get; set; }

    /// <summary>
    /// AniDB episode id.
    /// </summary>
    public int AnidbEpisodeID { get; set; }

    /// <summary>
    /// Shoko series id.
    /// </summary>
    public int? SeriesID { get; set; }

    /// <summary>
    /// AniDB anime id.
    /// </summary>
    public int AnidbAnimeID { get; set; }

    /// <summary>
    /// Shoko group id.
    /// </summary>
    public int? GroupID { get; set; }
}
