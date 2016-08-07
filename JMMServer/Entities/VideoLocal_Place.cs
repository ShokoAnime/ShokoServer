using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMContracts;
using JMMServer.FileHelper;
using JMMServer.Repositories;
using NLog;
using NutzCode.CloudFileSystem;

namespace JMMServer.Entities
{
    public class VideoLocal_Place
    {
        public int VideoLocal_Place_ID { get; private set; }
        public int VideoLocalID { get; set; }
        public string FilePath { get; set; }
        public int ImportFolderID { get; set; }
        public int ImportFolderType { get; set; }

        public ImportFolder ImportFolder => new ImportFolderRepository().GetByID(ImportFolderID);
        public string FullServerPath => Path.Combine(ImportFolder.ImportFolderLocation, FilePath);
        public VideoLocal VideoLocal => new VideoLocalRepository().GetByID(VideoLocalID);

        private static Logger logger = LogManager.GetCurrentClassLogger();

        public void RenameFile(string renameScript)
        {
            string renamed = RenameFileHelper.GetNewFileName(VideoLocal, renameScript);
            if (string.IsNullOrEmpty(renamed)) return;

            ImportFolderRepository repFolders = new ImportFolderRepository();
            VideoLocal_PlaceRepository repVids = new VideoLocal_PlaceRepository();
            IFileSystem filesys = ImportFolder.FileSystem;
            if (filesys == null)
                return;
            // actually rename the file
            string fullFileName = this.FullServerPath;

            // check if the file exists



            FileSystemResult<IObject> re = filesys.Resolve(fullFileName);
            if ((re == null) || (!re.IsOk))
            {
                logger.Error("Error could not find the original file for renaming: " + fullFileName);
                return;
            }
            IObject file = re.Result;
            // actually rename the file
            string path = Path.GetDirectoryName(fullFileName);
            string newFullName = Path.Combine(path, renamed);

            try
            {
                logger.Info($"Renaming file From ({fullFileName}) to ({newFullName})....");

                if (fullFileName.Equals(newFullName, StringComparison.InvariantCultureIgnoreCase))
                {
                    logger.Info($"Renaming file SKIPPED, no change From ({fullFileName}) to ({newFullName})");
                }
                else
                {
                    FileSystemResult r = file.Rename(renamed);
                    if (r.IsOk)
                    {
                        logger.Info($"Renaming file SUCCESS From ({fullFileName}) to ({newFullName})");
                        Tuple<ImportFolder, string> tup = VideoLocal_PlaceRepository.GetFromFullPath(newFullName);
                        if (tup == null)
                        {
                            logger.Error($"Unable to locate file {newFullName} inside the import folders");
                            return;
                        }
                        this.FilePath = tup.Item2;
                        repVids.Save(this);
                    }
                    else
                    {
                        logger.Info($"Renaming file FAIL From ({fullFileName}) to ({newFullName}) - {r.Error}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Info($"Renaming file FAIL From ({fullFileName}) to ({newFullName}) - {ex.Message}");
                logger.ErrorException(ex.ToString(), ex);
            }
        }
        public IFile GetFile()
        {
            IFileSystem fs = ImportFolder.FileSystem;
            if (fs == null)
                return null;
            FileSystemResult<IObject> fobj = fs.Resolve(FullServerPath);
            if (!fobj.IsOk || fobj.Result is IDirectory)
                return null;
            return fobj.Result as IFile;
        }
        public bool RefreshMediaInfo()
        {
            return MediaInfoReader.ReadMediaInfo(this);
        }
        public void RenameIfRequired()
        {
            try
            {
                RenameScriptRepository repScripts = new RenameScriptRepository();
                RenameScript defaultScript = repScripts.GetDefaultScript();

                if (defaultScript == null) return;

                RenameFile(defaultScript.Script);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return;
            }
        }

        public void MoveFileIfRequired()
        {
            try
            {
                logger.Trace("Attempting to move file: {0}", this.FullServerPath);

                // check if this file is in the drop folder
                // otherwise we don't need to move it
                if (ImportFolder.IsDropSource == 0)
                {
                    logger.Trace("Not moving file as it is NOT in the drop folder: {0}", this.FullServerPath);
                    return;
                }
                IFileSystem f = this.ImportFolder.FileSystem;
                if (f == null)
                {
                    logger.Trace("Unable to move, filesystem not working: {0}", this.FullServerPath);
                    return;

                }

                FileSystemResult<IObject> fsrresult = f.Resolve(FullServerPath);
                if (!fsrresult.IsOk)
                {
                    logger.Error("Could not find the file to move: {0}", this.FullServerPath);
                    return;
                }
                IFile source_file = fsrresult.Result as IFile;
                if (source_file == null)
                {
                    logger.Error("Could not find the file to move: {0}", this.FullServerPath);
                    return;
                }
                // find the default destination
                ImportFolder destFolder = null;
                ImportFolderRepository repFolders = new ImportFolderRepository();
                foreach (ImportFolder fldr in repFolders.GetAll().Where(a => a.CloudID == ImportFolder.CloudID))
                {
                    if (fldr.IsDropDestination == 1)
                    {
                        destFolder = fldr;
                        break;
                    }
                }

                if (destFolder == null) return;

                FileSystemResult<IObject> re = f.Resolve(destFolder.ImportFolderLocation);
                if (!re.IsOk)
                    return;

                // keep the original drop folder for later (take a copy, not a reference)
                ImportFolder dropFolder = this.ImportFolder;

                // we can only move the file if it has an anime associated with it
                List<CrossRef_File_Episode> xrefs = this.VideoLocal.EpisodeCrossRefs;
                if (xrefs.Count == 0) return;
                CrossRef_File_Episode xref = xrefs[0];

                // find the series associated with this episode
                AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
                AnimeSeries series = repSeries.GetByAnimeID(xref.AnimeID);
                if (series == null) return;

                // find where the other files are stored for this series
                // if there are no other files except for this one, it means we need to create a new location
                bool foundLocation = false;
                string newFullPath = "";

                // sort the episodes by air date, so that we will move the file to the location of the latest episode
                List<AnimeEpisode> allEps = series.GetAnimeEpisodes().OrderByDescending(a => a.AniDB_EpisodeID).ToList();

                AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
                CrossRef_File_EpisodeRepository repFileEpXref = new CrossRef_File_EpisodeRepository();
                IDirectory destination = null;

                foreach (AnimeEpisode ep in allEps)
                {
                    // check if this episode belongs to more than one anime
                    // if it does we will ignore it
                    List<CrossRef_File_Episode> fileEpXrefs = repFileEpXref.GetByEpisodeID(ep.AniDB_EpisodeID);
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

                    foreach (VideoLocal vid in ep.GetVideoLocals().Where(a => a.Places.Any(b=>b.ImportFolder.CloudID == destFolder.CloudID && b.ImportFolder.IsDropSource==0)))
                    {
                        if (vid.VideoLocalID != this.VideoLocalID)
                        {
                            VideoLocal_Place place=vid.Places.FirstOrDefault(a=>a.ImportFolder.CloudID==destFolder.CloudID);
                            string thisFileName = place?.FullServerPath;
                            string folderName = Path.GetDirectoryName(thisFileName);

                            FileSystemResult<IObject> dir = f.Resolve(folderName);
                            if (dir.IsOk)
                            {
                                destination = (IDirectory)dir.Result;
                                newFullPath = folderName;
                                foundLocation = true;
                                break;
                            }
                        }
                    }
                    if (foundLocation) break;
                }

                if (!foundLocation)
                {
                    // we need to create a new folder
                    string newFolderName = Utils.RemoveInvalidFolderNameCharacters(series.GetAnime().MainTitle);
                    newFullPath = Path.Combine(destFolder.ImportFolderLocation, newFolderName);
                    FileSystemResult<IObject> dirn = f.Resolve(newFullPath);
                    if (!dirn.IsOk)
                    {
                        dirn = f.Resolve(destFolder.ImportFolderLocation);
                        if (dirn.IsOk)
                        {
                            IDirectory d = (IDirectory)dirn.Result;
                            FileSystemResult<IDirectory> d2 = Task.Run(async () => await d.CreateDirectoryAsync(newFolderName, null)).Result;
                            destination = d2.Result;

                        }
                    }
                    else if (dirn.Result is IFile)
                    {
                        logger.Error("Destination folder is a file: {0}", newFolderName);
                    }
                }
                VideoLocal_PlaceRepository repVids = new VideoLocal_PlaceRepository();

                //int newFolderID = 0;
                //string newPartialPath = "";
                string newFullServerPath = Path.Combine(newFullPath, Path.GetFileName(this.FullServerPath));
                Tuple<ImportFolder, string> tup = VideoLocal_PlaceRepository.GetFromFullPath(newFullServerPath);
                if (tup == null)
                {
                    logger.Error($"Unable to locate file {newFullServerPath} inside the import folders");
                    return;
                }
                logger.Info("Moving file from {0} to {1}", this.FullServerPath, newFullServerPath);

                FileSystemResult<IObject> dst = f.Resolve(newFullServerPath);
                if (dst.IsOk)
                {
                    logger.Trace("Not moving file as it already exists at the new location, deleting source file instead: {0} --- {1}",
                        this.FullServerPath, newFullServerPath);

                    // if the file already exists, we can just delete the source file instead
                    // this is safer than deleting and moving
                    FileSystemResult fr = source_file.Delete(true);
                    if (!fr.IsOk)
                    {
                        logger.Warn("Unable to delete file: {0} error {1}", this.FullServerPath, fr?.Error ?? String.Empty);
                    }
                    this.ImportFolderID = tup.Item1.ImportFolderID;
                    this.FilePath = tup.Item2;
                    repVids.Save(this);
                }
                else
                {
                    FileSystemResult fr = source_file.Move(destination);
                    if (!fr.IsOk)
                    {
                        logger.Error("Unable to move file: {0} to {1} error {2)", this.FullServerPath, newFullServerPath, fr?.Error ?? String.Empty);
                        return;
                    }
                    string originalFileName = this.FullServerPath;


                    this.ImportFolderID = tup.Item1.ImportFolderID;
                    this.FilePath = tup.Item2;
                    repVids.Save(this);

                    try
                    {
                        // move any subtitle files
                        foreach (string subtitleFile in Utils.GetPossibleSubtitleFiles(originalFileName))
                        {
                            FileSystemResult<IObject> src = f.Resolve(subtitleFile);
                            if (src.IsOk && src.Result is IFile)
                            {
                                string newSubPath = Path.Combine(Path.GetDirectoryName(newFullServerPath), ((IFile)src).Name);
                                dst = f.Resolve(newSubPath);
                                if (dst != null && dst.IsOk && dst.Result is IFile)
                                {
                                    FileSystemResult fr2 = src.Result.Delete(true);
                                    if (!fr2.IsOk)
                                    {
                                        logger.Warn("Unable to delete file: {0} error {1}", subtitleFile,
                                            fr2?.Error ?? String.Empty);
                                    }
                                }
                                else
                                {
                                    FileSystemResult fr2 = ((IFile)src).Move(destination);
                                    if (!fr2.IsOk)
                                    {
                                        logger.Error("Unable to move file: {0} to {1} error {2)", subtitleFile,
                                            newSubPath, fr2?.Error ?? String.Empty);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.ErrorException(ex.ToString(), ex);
                    }

                    // check for any empty folders in drop folder
                    // only for the drop folder
                    if (dropFolder.IsDropSource == 1)
                    {
                        FileSystemResult<IObject> dd = f.Resolve(dropFolder.ImportFolderLocation);
                        if (dd != null && dd.IsOk && dd.Result is IDirectory)
                            RecursiveDeleteEmptyDirectories((IDirectory)dd.Result,true);
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"Could not move file: {this.FullServerPath} -- {ex.ToString()}";
                logger.ErrorException(msg, ex);
            }
        }

        public Contract_VideoLocal_Place ToContract()
        {
            Contract_VideoLocal_Place v = new Contract_VideoLocal_Place
            {
                FilePath = FilePath,
                ImportFolderID = ImportFolderID,
                ImportFolderType = ImportFolderType,
                VideoLocalID = VideoLocalID,
                ImportFolder = ImportFolder.ToContract(),
                VideoLocal_Place_ID = VideoLocal_Place_ID
            };
            return v;
        }
        private void RecursiveDeleteEmptyDirectories(IDirectory dir, bool importfolder)
        {
            FileSystemResult fr = dir.Populate();
            if (fr.IsOk)
            {
                if (dir.Files.Count > 0)
                    return;
                foreach (IDirectory d in dir.Directories)
                    RecursiveDeleteEmptyDirectories(d,false);
            }
            if (importfolder)
                return;
            fr = dir.Refresh();
            if (fr.IsOk)
            {
                if (dir.Files.Count == 0 && dir.Directories.Count == 0)
                {
                    fr = dir.Delete(true);
                    if (!fr.IsOk)
                    {
                        logger.Warn("Unable to delete directory: {0} error {1}", dir.FullName, fr?.Error ?? String.Empty);
                    }
                }
            }
        }

    }
}
