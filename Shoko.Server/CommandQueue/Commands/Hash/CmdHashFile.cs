using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NutzCode.CloudFileSystem;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Models.WebCache;
using Shoko.Server.CommandQueue.Commands.Server;
using Shoko.Server.Models;
using Shoko.Server.Native.Hashing;
using Shoko.Server.Providers.WebCache;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Repos;
using Shoko.Server.Utilities;

namespace Shoko.Server.CommandQueue.Commands.Hash
{
    public class CmdHashFile : BaseCommand, ICommand
    {
        public static HashTypes HashAll = HashTypes.CRC | HashTypes.ED2K | HashTypes.MD5 | HashTypes.SHA1;
        private readonly string _filePath;
        private readonly SVR_ImportFolder _importFolder;

        private bool _hashingState;

        private string _parallelTag;


        public CmdHashFile(string str) : base(str)
        {
            (SVR_ImportFolder folder, string filePath) = VideoLocal_PlaceRepository.GetFromFullPath(FullName);
            if (folder == null)
                throw new IOException($"Unable to locate Import Folder for {FullName}");
            IFileSystem f = folder.FileSystem;
            if (f == null)
                throw new IOException($"Unable to open filesystem for: {FullName}");
            _importFolder = folder;
            _filePath = filePath;
            IObject source = f.Resolve(FullName);
            if (source == null || source.Status != NutzCode.CloudFileSystem.Status.Ok || !(source is IFile source_file))
                throw new IOException($"Could not access file: {FullName}");
            File = source_file;
        }


        public CmdHashFile(string filename, bool force)
        {
            (SVR_ImportFolder folder, string filePath) = VideoLocal_PlaceRepository.GetFromFullPath(filename);
            if (folder == null)
                throw new IOException($"Unable to locate Import Folder for {filename}");
            IFileSystem f = folder.FileSystem;
            if (f == null)
                throw new IOException($"Unable to open filesystem for: {filename}");
            _importFolder = folder;
            _filePath = filePath;
            IObject source = f.Resolve(filename);
            if (source == null || source.Status != NutzCode.CloudFileSystem.Status.Ok || !(source is IFile source_file))
                throw new IOException($"Could not access file: {filename}");
            File = source_file;
            Force = force;
            FileSystemName = File.FileSystem.Name;
            FullName = File.FullName;
        }

        public CmdHashFile(IFile file, bool force)
        {
            File = file;
            Force = force;
            FileSystemName = File.FileSystem.Name;
            FullName = File.FullName;
            (SVR_ImportFolder folder, string filePath) = VideoLocal_PlaceRepository.GetFromFullPath(File.FullName);
            _importFolder = folder;
            _filePath = filePath;
        }

        public bool Force { get; set; }
        public string FileSystemName { get; set; }
        public string FullName { get; set; }

        private IFile File { get; }
        public QueueStateStruct PrettyDescription => new QueueStateStruct {QueueState = _hashingState ? QueueStateEnum.HashingFile : QueueStateEnum.CheckingFile, ExtraParams = new[] {File.FullName}};
        public int Priority { get; set; } = 4;
        public string Id => $"HashFile_{FullName}";
        public WorkTypes WorkType => WorkTypes.Hashing;

        public string ParallelTag
        {
            get
            {
                if (!string.IsNullOrEmpty(_parallelTag))
                    return _parallelTag;
                if (!string.IsNullOrEmpty(_importFolder.PhysicalTag))
                    return _importFolder.PhysicalTag;
                return string.Empty;
            }
            set { _parallelTag = value; }
        }

        public int ParallelMax { get; set; } = 1;

        public override async Task RunAsync(IProgress<ICommand> progress = null, CancellationToken token = default(CancellationToken))
        {
            logger.Trace($"Checking File For Hashes: {File.FullName}");

            try
            {
                ReportInit(progress);
                // hash and read media info for file
                int nshareID;

                long filesize = 0;
                if (_importFolder.CloudID == null) // Local Access
                {
                    if (!System.IO.File.Exists(File.FullName))
                    {
                        ReportError(progress, $"File does not exist: {File.FullName}");
                        return;
                    }

                    int numAttempts = 0;
                    bool writeAccess = _importFolder.IsDropSource == 1;

                    // Wait 1 minute before giving up on trying to access the file
                    while ((filesize = CanAccessFile(File.FullName, writeAccess)) == 0 && numAttempts < 60)
                    {
                        numAttempts++;
                        await Task.Delay(1000);
                        logger.Trace($@"Failed to access, (or filesize is 0) Attempt # {numAttempts}, {File.FullName}");
                    }

                    // if we failed to access the file, get ouuta here
                    if (numAttempts >= 60)
                    {
                        ReportError(progress, $"Could not access file: {File.FullName}");
                        return;
                    }

                    // At least 1s between to ensure that size has the chance to change
                    await Task.Delay(1000);
                    numAttempts = 0;

                    //For systems with no locking
                    while (FileModified(File.FullName, 3, ref filesize, writeAccess) && numAttempts < 60)
                    {
                        numAttempts++;
                        await Task.Delay(1000);
                        // Only show if it's more than 3s past
                        if (numAttempts > 3) logger.Warn($@"The modified date is too soon. Waiting to ensure that no processes are writing to it. {numAttempts}/60 {File.FullName}");
                    }

                    // if we failed to access the file, get ouuta here
                    if (numAttempts >= 60)
                    {
                        ReportError(progress, $"Could not access file: {File.FullName}");
                        return;
                    }
                }

                ReportUpdate(progress, 10);
                if (_importFolder.CloudID.HasValue)
                    filesize = File.Size;
                nshareID = _importFolder.ImportFolderID;


                // check if we have already processed this file
                SVR_VideoLocal_Place vlocalplace = Repo.Instance.VideoLocal_Place.GetByFilePathAndImportFolderID(_filePath, nshareID);
                SVR_VideoLocal vlocal = null;
                var filename = Path.GetFileName(_filePath);

                if (vlocalplace != null)
                {
                    vlocal = vlocalplace.VideoLocal;
                    if (vlocal != null)
                    {
                        logger.Trace("VideoLocal record found in database: {0}", File.FullName);

                        // This will only happen with DB corruption, so just clean up the mess.
                        if (vlocalplace.FullServerPath == null)
                        {
                            if (vlocal.Places.Count == 1)
                            {
                                Repo.Instance.VideoLocal.Delete(vlocal);
                                vlocal = null;
                            }

                            Repo.Instance.VideoLocal_Place.Delete(vlocalplace);
                            vlocalplace = null;
                        }

                        if (vlocal != null && Force)
                        {
                            vlocal.FileSize = filesize;
                            vlocal.DateTimeUpdated = DateTime.Now;
                        }
                    }
                }

                bool duplicate = false;

                SVR_VideoLocal vlocal1 = vlocal;
                using (var txn = Repo.Instance.VideoLocal.BeginAddOrUpdate(() => Repo.Instance.VideoLocal.GetByID(vlocal1?.VideoLocalID ?? 0), () =>
                {
                    logger.Trace("No existing VideoLocal, creating temporary record");
                    return new SVR_VideoLocal
                    {
                        DateTimeUpdated = DateTime.Now,
                        DateTimeCreated = DateTime.Now,
                        FileSize = filesize,
                        Hash = string.Empty,
                        CRC32 = string.Empty,
                        MD5 = File?.MD5?.ToUpperInvariant() ?? string.Empty,
                        SHA1 = File?.SHA1?.ToUpperInvariant() ?? string.Empty,
                        IsIgnored = 0,
                        IsVariation = 0
                    };
                }))
                {
                    vlocal = txn.Entity;
                    if (vlocalplace == null)
                    {
                        logger.Trace("No existing VideoLocal_Place, creating a new record");
                        vlocalplace = new SVR_VideoLocal_Place {FilePath = _filePath, ImportFolderID = nshareID, ImportFolderType = _importFolder.ImportFolderType};
                        // Make sure we have an ID
                        vlocalplace = Repo.Instance.VideoLocal_Place.BeginAdd(vlocalplace).Commit();
                    }

                    // check if we need to get a hash this file
                    // IDEs might warn of possible null. It is set in the lambda above, so it shouldn't ever be null
                    if (string.IsNullOrEmpty(vlocal.Hash) || Force)
                    {
                        logger.Trace("No existing hash in VideoLocal, checking XRefs");
                        if (!Force)
                        {
                            // try getting the hash from the CrossRef
                            List<CrossRef_File_Episode> crossRefs = Repo.Instance.CrossRef_File_Episode.GetByFileNameAndSize(filename, vlocal.FileSize);
                            if (crossRefs.Any())
                            {
                                vlocal.Hash = crossRefs[0].Hash;
                                vlocal.HashSource = (int) HashSource.DirectHash;
                            }
                        }

                        // try getting the hash from the LOCAL cache
                        if (!Force && string.IsNullOrEmpty(vlocal.Hash))
                        {
                            List<FileNameHash> fnhashes = Repo.Instance.FileNameHash.GetByFileNameAndSize(filename, vlocal.FileSize);
                            if (fnhashes != null && fnhashes.Count > 1)
                            {
                                // if we have more than one record it probably means there is some sort of corruption
                                // lets delete the local records
                                foreach (FileNameHash fnh in fnhashes)
                                {
                                    Repo.Instance.FileNameHash.Delete(fnh.FileNameHashID);
                                }
                            }

                            // reinit this to check if we erased them
                            fnhashes = Repo.Instance.FileNameHash.GetByFileNameAndSize(filename, vlocal.FileSize);

                            if (fnhashes != null && fnhashes.Count == 1)
                            {
                                logger.Trace("Got hash from LOCAL cache: {0} ({1})", File.FullName, fnhashes[0].Hash);
                                vlocal.Hash = fnhashes[0].Hash;
                                vlocal.HashSource = (int) HashSource.WebCacheFileName;
                            }
                        }

                        if (string.IsNullOrEmpty(vlocal.Hash))
                            FillVideoHashes(vlocal);

                        //Cloud and no hash, Nothing to do, except maybe Get the mediainfo....
                        if (string.IsNullOrEmpty(vlocal.Hash) && _importFolder.CloudID.HasValue)
                        {
                            logger.Trace("No Hash found for cloud " + filename + " putting in videolocal table with empty ED2K");
                            vlocal = txn.Commit(true);
                            int vlpid = vlocalplace.VideoLocalID;
                            using (var upd = Repo.Instance.VideoLocal_Place.BeginAddOrUpdate(() => Repo.Instance.VideoLocal_Place.GetByID(vlpid)))
                            {
                                upd.Entity.VideoLocalID = vlocal.VideoLocalID;
                                vlocalplace = upd.Commit();
                            }

                            if (vlocalplace.RefreshMediaInfo(vlocal))
                                txn.Commit(true);
                            ReportFinish(progress);
                            return;
                        }

                        // hash the file
                        if (string.IsNullOrEmpty(vlocal.Hash) || Force)
                        {
                            logger.Info("Hashing File: {0}", File.FullName);
                            _hashingState = true;
                            DateTime start = DateTime.Now;
                            // update the VideoLocal record with the Hash, since cloud support we calculate everything
                            Hasher h = new Hasher(File, HashAll);
                            string error = await h.RunAsync(new ChildProgress(20, 60, this, progress), token);
                            if (error != null)
                            {
                                ReportError(progress, error);
                                return;
                            }

                            TimeSpan ts = DateTime.Now - start;
                            logger.Trace("Hashed file in {0:#0.0} seconds --- {1} ({2})", ts.TotalSeconds, File.FullName, Utils.FormatByteSize(vlocal.FileSize));
                            vlocal.Hash = h.Result.GetHash(HashTypes.ED2K);
                            vlocal.CRC32 = h.Result.GetHash(HashTypes.CRC);
                            vlocal.MD5 = h.Result.GetHash(HashTypes.MD5);
                            vlocal.SHA1 = h.Result.GetHash(HashTypes.SHA1);
                            vlocal.HashSource = (int) HashSource.DirectHash;
                        }

                        _hashingState = false;
                        await FillMissingHashes(vlocal, token, progress);

                        // We should have a hash by now
                        // before we save it, lets make sure there is not any other record with this hash (possible duplicate file)
                        // TODO Check this case. I'm not sure how EF handles changing objects that we are working on
                        SVR_VideoLocal tlocal = Repo.Instance.VideoLocal.GetByHash(vlocal.Hash);

                        bool changed = false;

                        if (tlocal != null)
                        {
                            logger.Trace("Found existing VideoLocal with hash, merging info from it");
                            // Aid with hashing cloud. Merge hashes and save, regardless of duplicate file
                            changed = tlocal.MergeInfoFrom(vlocal);
                            vlocal = tlocal;

                            List<SVR_VideoLocal_Place> preps = vlocal.Places.Where(a => a.ImportFolder.CloudID == _importFolder.CloudID && !vlocalplace.FullServerPath.Equals(a.FullServerPath)).ToList();
                            foreach (var prep in preps)
                            {
                                if (prep == null) continue;
                                // clean up, if there is a 'duplicate file' that is invalid, remove it.
                                if (prep.FullServerPath == null)
                                {
                                    Repo.Instance.VideoLocal_Place.Delete(prep);
                                }
                                else
                                {
                                    IResult dupFileSystemResult = prep.ImportFolder?.FileSystem?.Resolve(prep.FullServerPath);
                                    if (dupFileSystemResult == null || dupFileSystemResult.Status != NutzCode.CloudFileSystem.Status.Ok)
                                        Repo.Instance.VideoLocal_Place.Delete(prep);
                                }
                            }

                            var dupPlace = vlocal.Places.FirstOrDefault(a => a.ImportFolder.CloudID == _importFolder.CloudID && !vlocalplace.FullServerPath.Equals(a.FullServerPath));
                            ReportUpdate(progress, 85);
                            if (dupPlace != null)
                            {
                                logger.Warn("Found Duplicate File");
                                logger.Warn("---------------------------------------------");
                                logger.Warn($"New File: {vlocalplace.FullServerPath}");
                                logger.Warn($"Existing File: {dupPlace.FullServerPath}");
                                logger.Warn("---------------------------------------------");

                                // check if we have a record of this in the database, if not create one
                                List<DuplicateFile> dupFiles = Repo.Instance.DuplicateFile.GetByFilePathsAndImportFolder(vlocalplace.FilePath, dupPlace.FilePath, vlocalplace.ImportFolderID, dupPlace.ImportFolderID);
                                if (dupFiles.Count == 0)
                                    dupFiles = Repo.Instance.DuplicateFile.GetByFilePathsAndImportFolder(dupPlace.FilePath, vlocalplace.FilePath, dupPlace.ImportFolderID, vlocalplace.ImportFolderID);

                                if (dupFiles.Count == 0)
                                {
                                    DuplicateFile dup = new DuplicateFile
                                    {
                                        DateTimeUpdated = DateTime.Now,
                                        FilePathFile1 = vlocalplace.FilePath,
                                        FilePathFile2 = dupPlace.FilePath,
                                        ImportFolderIDFile1 = vlocalplace.ImportFolderID,
                                        ImportFolderIDFile2 = dupPlace.ImportFolderID,
                                        Hash = vlocal.Hash
                                    };
                                    Repo.Instance.DuplicateFile.BeginAdd(dup).Commit();
                                }

                                //Notify duplicate, don't delete
                                duplicate = true;
                            }
                        }

                        if (!duplicate || changed)
                            vlocal = txn.Commit();
                    }

                    ReportUpdate(progress, 90);

                    int vlplid = vlocalplace.VideoLocalID;
                    using (var upd = Repo.Instance.VideoLocal_Place.BeginAddOrUpdate(() => Repo.Instance.VideoLocal_Place.GetByID(vlplid)))
                    {
                        upd.Entity.VideoLocalID = vlocal.VideoLocalID;
                        vlocalplace = upd.Commit();
                    }
                }

                if (duplicate)
                {
                    Queue.Instance.Add(new CmdServerProcessFile(vlocal.VideoLocalID, false));
                    ReportFinish(progress);
                    return;
                }

                // also save the filename to hash record
                // replace the existing records just in case it was corrupt
                List<FileNameHash> fnhashes2 = Repo.Instance.FileNameHash.GetByFileNameAndSize(filename, vlocal.FileSize);
                if (fnhashes2 != null && fnhashes2.Count > 1)
                {
                    // if we have more than one record it probably means there is some sort of corruption
                    // lets delete the local records
                    foreach (FileNameHash fnh in fnhashes2)
                    {
                        Repo.Instance.FileNameHash.Delete(fnh.FileNameHashID);
                    }
                }

                ReportUpdate(progress, 95);

                using (var upd = Repo.Instance.FileNameHash.BeginAddOrUpdate(() => fnhashes2?.Count == 1 ? fnhashes2[0] : null))
                {
                    upd.Entity.FileName = filename;
                    upd.Entity.FileSize = vlocal.FileSize;
                    upd.Entity.Hash = vlocal.Hash;
                    upd.Entity.DateTimeUpdated = DateTime.Now;
                    upd.Commit();
                }

                if (vlocal.Media == null || vlocal.MediaVersion < SVR_VideoLocal.MEDIA_VERSION || vlocal.Duration == 0)
                {
                    int vid = vlocal.VideoLocalID;
                    using (var upd = Repo.Instance.VideoLocal.BeginAddOrUpdate(() => Repo.Instance.VideoLocal.GetByID(vid)))
                        if (vlocalplace.RefreshMediaInfo(upd.Entity))
                            vlocal = upd.Commit(true);
                }

                // now add a command to process the file
                Queue.Instance.Add(new CmdServerProcessFile(vlocal.VideoLocalID, false));
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing ServerHashFile: {File.FullName}\n{ex}", ex);
            }
        }


        //Added size return, since symbolic links return 0, we use this function also to return the size of the file.
        private long CanAccessFile(string fileName, bool writeAccess)
        {
            var accessType = writeAccess ? FileAccess.ReadWrite : FileAccess.Read;
            try
            {
                using (FileStream fs = System.IO.File.Open(fileName, FileMode.Open, accessType, FileShare.None))
                {
                    long size = fs.Seek(0, SeekOrigin.End);
                    return size;
                }
            }
            catch
            {
                return 0;
            }
        }

        //Used to check if file has been modified within the last X seconds.
        private bool FileModified(string FileName, int Seconds, ref long lastFileSize, bool writeAccess)
        {
            try
            {
                var lastWrite = System.IO.File.GetLastWriteTime(FileName);
                var now = DateTime.Now;
                // check that the size is also equal, since some copy utilities apply the previous modified date
                var size = CanAccessFile(FileName, writeAccess);
                if (lastWrite <= now && lastWrite.AddSeconds(Seconds) >= now || lastFileSize != size)
                {
                    lastFileSize = size;
                    return true;
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }


        private async Task FillMissingHashes(SVR_VideoLocal vlocal, CancellationToken token, IProgress<ICommand> progress = null)
        {
            HashTypes types = 0;
            if (string.IsNullOrEmpty(vlocal.CRC32))
                types |= HashTypes.CRC;
            if (string.IsNullOrEmpty(vlocal.MD5))
                types |= HashTypes.MD5;
            if (string.IsNullOrEmpty(vlocal.SHA1))
                types |= HashTypes.SHA1;
            if (types > 0)
                FillVideoHashes(vlocal);
            types = 0;
            if (string.IsNullOrEmpty(vlocal.CRC32))
                types |= HashTypes.CRC;
            if (string.IsNullOrEmpty(vlocal.MD5))
                types |= HashTypes.MD5;
            if (string.IsNullOrEmpty(vlocal.SHA1))
                types |= HashTypes.SHA1;
            if (types > 0)
            {
                _hashingState = true;
                DateTime start = DateTime.Now;
                logger.Trace("Calculating missing {1} hashes for: {0}", File.FullName, types.ToString("F"));
                // update the VideoLocal record with the Hash, since cloud support we calculate everything
                Hasher h = new Hasher(File, HashAll);
                string error = await h.RunAsync(new ChildProgress(20, 60, this, progress), token);
                TimeSpan ts = DateTime.Now - start;
                logger.Trace("Hashed file in {0:#0.0} seconds --- {1} ({2})", ts.TotalSeconds, File.FullName, Utils.FormatByteSize(vlocal.FileSize));
                if (error != null)
                    logger.Error("Unable to add additional hashes missing {1} hashes for: {0} Error {2}", File.FullName, types.ToString("F"), error);
                else
                {
                    if ((types & HashTypes.CRC) > 0)
                        vlocal.CRC32 = h.Result.GetHash(HashTypes.CRC);
                    if ((types & HashTypes.MD5) > 0)
                        vlocal.MD5 = h.Result.GetHash(HashTypes.MD5);
                    if ((types & HashTypes.SHA1) > 0)
                        vlocal.SHA1 = h.Result.GetHash(HashTypes.SHA1);
                    WebCacheAPI.Send_FileHash(new List<SVR_VideoLocal> {vlocal});
                }
            }
        }

        private void FillHashesAgainstVideoLocalRepo(SVR_VideoLocal v)
        {
            if (!string.IsNullOrEmpty(v.ED2KHash))
            {
                SVR_VideoLocal n = Repo.Instance.VideoLocal.GetByHash(v.ED2KHash);
                if (n != null)
                {
                    if (!string.IsNullOrEmpty(n.CRC32))
                        v.CRC32 = n.CRC32.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(n.MD5))
                        v.MD5 = n.MD5.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(n.SHA1))
                        v.SHA1 = n.SHA1.ToUpperInvariant();
                    return;
                }
            }

            if (!string.IsNullOrEmpty(v.SHA1))
            {
                SVR_VideoLocal n = Repo.Instance.VideoLocal.GetBySHA1(v.SHA1);
                if (n != null)
                {
                    if (!string.IsNullOrEmpty(n.CRC32))
                        v.CRC32 = n.CRC32.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(n.MD5))
                        v.MD5 = n.MD5.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(v.ED2KHash))
                        v.ED2KHash = n.ED2KHash.ToUpperInvariant();
                    return;
                }
            }

            if (!string.IsNullOrEmpty(v.MD5))
            {
                SVR_VideoLocal n = Repo.Instance.VideoLocal.GetByMD5(v.MD5);
                if (n != null)
                {
                    if (!string.IsNullOrEmpty(n.CRC32))
                        v.CRC32 = n.CRC32.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(n.SHA1))
                        v.SHA1 = n.SHA1.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(v.ED2KHash))
                        v.ED2KHash = n.ED2KHash.ToUpperInvariant();
                }
            }
        }

        private void FillHashesAgainstAniDBRepo(SVR_VideoLocal v)
        {
            if (!string.IsNullOrEmpty(v.ED2KHash))
            {
                SVR_AniDB_File f = Repo.Instance.AniDB_File.GetByHash(v.ED2KHash);
                if (f != null)
                {
                    if (!string.IsNullOrEmpty(f.CRC))
                        v.CRC32 = f.CRC.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(f.SHA1))
                        v.SHA1 = f.SHA1.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(f.MD5))
                        v.MD5 = f.MD5.ToUpperInvariant();
                    return;
                }
            }

            if (!string.IsNullOrEmpty(v.SHA1))
            {
                SVR_AniDB_File f = Repo.Instance.AniDB_File.GetBySHA1(v.SHA1);
                if (f != null)
                {
                    if (!string.IsNullOrEmpty(f.CRC))
                        v.CRC32 = f.CRC.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(f.Hash))
                        v.ED2KHash = f.Hash.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(f.MD5))
                        v.MD5 = f.MD5.ToUpperInvariant();
                    return;
                }
            }

            if (!string.IsNullOrEmpty(v.MD5))
            {
                SVR_AniDB_File f = Repo.Instance.AniDB_File.GetByMD5(v.MD5);
                if (f != null)
                {
                    if (!string.IsNullOrEmpty(f.CRC))
                        v.CRC32 = f.CRC.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(f.Hash))
                        v.ED2KHash = f.Hash.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(f.SHA1))
                        v.SHA1 = f.SHA1.ToUpperInvariant();
                }
            }
        }

        private void FillHashesAgainstWebCache(SVR_VideoLocal v)
        {
            if (!string.IsNullOrEmpty(v.ED2KHash))
            {
                List<WebCache_FileHash> ls = WebCacheAPI.Get_FileHash(FileHashType.ED2K, v.ED2KHash) ?? new List<WebCache_FileHash>();
                ls = ls.Where(a => !string.IsNullOrEmpty(a.CRC32) && !string.IsNullOrEmpty(a.MD5) && !string.IsNullOrEmpty(a.SHA1)).ToList();
                if (ls.Count > 0)
                {
                    if (!string.IsNullOrEmpty(ls[0].SHA1))
                        v.SHA1 = ls[0].SHA1.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(ls[0].CRC32))
                        v.CRC32 = ls[0].CRC32.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(ls[0].MD5))
                        v.MD5 = ls[0].MD5.ToUpperInvariant();
                    return;
                }
            }

            if (!string.IsNullOrEmpty(v.SHA1))
            {
                List<WebCache_FileHash> ls = WebCacheAPI.Get_FileHash(FileHashType.SHA1, v.SHA1) ?? new List<WebCache_FileHash>();
                ls = ls.Where(a => !string.IsNullOrEmpty(a.CRC32) && !string.IsNullOrEmpty(a.MD5) && !string.IsNullOrEmpty(a.ED2K)).ToList();
                if (ls.Count > 0)
                {
                    if (!string.IsNullOrEmpty(ls[0].ED2K))
                        v.ED2KHash = ls[0].ED2K.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(ls[0].CRC32))
                        v.CRC32 = ls[0].CRC32.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(ls[0].MD5))
                        v.MD5 = ls[0].MD5.ToUpperInvariant();
                    return;
                }
            }

            if (!string.IsNullOrEmpty(v.MD5))
            {
                List<WebCache_FileHash> ls = WebCacheAPI.Get_FileHash(FileHashType.MD5, v.MD5) ?? new List<WebCache_FileHash>();
                ls = ls.Where(a => !string.IsNullOrEmpty(a.CRC32) && !string.IsNullOrEmpty(a.SHA1) && !string.IsNullOrEmpty(a.ED2K)).ToList();
                if (ls.Count > 0)
                {
                    if (!string.IsNullOrEmpty(ls[0].ED2K))
                        v.ED2KHash = ls[0].ED2K.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(ls[0].CRC32))
                        v.CRC32 = ls[0].CRC32.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(ls[0].SHA1))
                        v.SHA1 = ls[0].SHA1.ToUpperInvariant();
                }
            }
        }

        private void FillVideoHashes(SVR_VideoLocal v)
        {
            if (string.IsNullOrEmpty(v.CRC32) || string.IsNullOrEmpty(v.MD5) || string.IsNullOrEmpty(v.SHA1))
                FillHashesAgainstVideoLocalRepo(v);
            if (string.IsNullOrEmpty(v.CRC32) || string.IsNullOrEmpty(v.MD5) || string.IsNullOrEmpty(v.SHA1))
                FillHashesAgainstAniDBRepo(v);
            if (string.IsNullOrEmpty(v.CRC32) || string.IsNullOrEmpty(v.MD5) || string.IsNullOrEmpty(v.SHA1))
                FillHashesAgainstWebCache(v);
        }
    }
}