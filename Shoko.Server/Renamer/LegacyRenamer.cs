using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.CloudFileSystem;
using Pri.LongPath;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Renamer
{
    class LegacyRenamer : IRenamer
    {
        RenameScript script;

        public LegacyRenamer(RenameScript script)
        {
            this.script = script;
        }

        public string GetFileName(SVR_VideoLocal_Place video)
        {
            return script == null ? null : RenameFileHelper.GetNewFileName(video.VideoLocal, script.Script);
        }

        public (ImportFolder dest, string folder) GetDestinationFolder(SVR_VideoLocal_Place video)
        {
            var sourceFile = video.ImportFolder.FileSystem.Resolve(video.FullServerPath).Result as IFile;

            ImportFolder destFolder = null;
            foreach (SVR_ImportFolder fldr in RepoFactory.ImportFolder.GetAll()
                .Where(a => a != null && a.CloudID == video.ImportFolder.CloudID).ToList())
            {
                if (!fldr.FolderIsDropDestination) continue;
                if (fldr.FolderIsDropSource) continue;
                IFileSystem fs = fldr.FileSystem;
                FileSystemResult<IObject> fsresult = fs?.Resolve(fldr.ImportFolderLocation);
                if (fsresult == null || !fsresult.IsOk) continue;

                // Continue if on a separate drive and there's no space
                if (!fldr.CloudID.HasValue && !video.ImportFolder.ImportFolderLocation.StartsWith(Path.GetPathRoot(fldr.ImportFolderLocation)))
                {
                    var fsresultquota = fs.Quota();
                    if (fsresultquota.IsOk && fsresultquota.Result.AvailableSize < sourceFile.Size) continue;
                }

                destFolder = fldr;
                break;
            }

            List<CrossRef_File_Episode> xrefs = video.VideoLocal.EpisodeCrossRefs;
            if (xrefs.Count == 0) return (destFolder, null);
            CrossRef_File_Episode xref = xrefs[0];

            // find the series associated with this episode
            SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(xref.AnimeID);



            // sort the episodes by air date, so that we will move the file to the location of the latest episode
            List<SVR_AnimeEpisode> allEps = series.GetAnimeEpisodes()
                .OrderByDescending(a => a.AniDB_Episode.AirDate)
                .ToList();

            IDirectory destination = null;

            foreach (SVR_AnimeEpisode ep in allEps)
            {
                // check if this episode belongs to more than one anime
                // if it does we will ignore it
                List<CrossRef_File_Episode> fileEpXrefs =
                    RepoFactory.CrossRef_File_Episode.GetByEpisodeID(ep.AniDB_EpisodeID);
                int? animeID = null;
                bool crossOver = false;
                foreach (CrossRef_File_Episode fileEpXref in fileEpXrefs)
                {
                    if (!animeID.HasValue)
                        animeID = fileEpXref.AnimeID;
                    else
                    {
                        if (animeID.Value != fileEpXref.AnimeID)
                            crossOver = true;
                    }
                }
                if (crossOver) continue;

                foreach (SVR_VideoLocal vid in ep.GetVideoLocals()
                    .Where(a => a.Places.Any(b => b.ImportFolder.CloudID == destFolder.CloudID &&
                                                  b.ImportFolder.IsDropSource == 0)).ToList())
                {
                    if (vid.VideoLocalID == video.VideoLocalID) continue;

                    SVR_VideoLocal_Place place =
                        vid.Places.FirstOrDefault(a => a.ImportFolder.CloudID == destFolder.CloudID);
                    string thisFileName = place?.FilePath;
                    if (thisFileName == null) continue;
                    string folderName = Path.GetDirectoryName(thisFileName);

                    FileSystemResult<IObject> dir = video.ImportFolder.FileSystem.Resolve(folderName);
                    if (!dir.IsOk) continue;
                    // ensure we aren't moving to the current directory
                    if (folderName.Equals(Path.GetDirectoryName(video.FullServerPath),
                        StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }
                    destination = dir.Result as IDirectory;
                    // Not a directory
                    if (destination == null) continue;

                    return (destFolder, folderName);
                }
            }

            return (destFolder, series.GetSeriesName());
        }
        public object FullServerPath { get; set; }
    }
}