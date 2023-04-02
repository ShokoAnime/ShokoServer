using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.FileHelper.Subtitles;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;
using Directory = System.IO.Directory;

namespace Shoko.Server.Models;

public class SVR_VideoLocal_Place : VideoLocal_Place, IVideoFile
{
    internal SVR_ImportFolder ImportFolder => RepoFactory.ImportFolder.GetByID(ImportFolderID);

    public string FullServerPath
    {
        get
        {
            if (string.IsNullOrEmpty(ImportFolder?.ImportFolderLocation) || string.IsNullOrEmpty(FilePath))
            {
                return null;
            }

            return Path.Combine(ImportFolder.ImportFolderLocation, FilePath);
        }
    }

    public string FileName => Path.GetFileName(FilePath);

    public SVR_VideoLocal VideoLocal => RepoFactory.VideoLocal.GetByID(VideoLocalID);

    private static Logger logger = LogManager.GetCurrentClassLogger();

    public FileInfo GetFileInfo()
    {
        if (!File.Exists(FullServerPath))
        {
            return null;
        }

        return new FileInfo(FullServerPath);
    }

    #region Relocation (Move & Rename)
    #region Records & Enums

    private enum DELAY_IN_USE
    {
        FIRST = 750,
        SECOND = 3000,
        THIRD = 5000,
    }

    /// <summary>
    /// Represents the outcome of a file relocation.
    /// </summary>
    /// <remarks>
    /// Possible states of the outcome:
    ///   Success: Success set to true, with valid ImportFolder and RelativePath
    ///     values.
    ///
    ///   Failure: Success set to false and ErrorMessage containing the reason.
    ///     ShouldRetry may be set to true if the operation can be retried.
    /// </remarks>
    public record RelocationResult
    {
        /// <summary>
        /// The relocation was successful.
        /// If true then the <see cref="ImportFolder"/> and
        /// <see cref="RelativePath"/> should be set to valid values, otherwise
        /// if false then the <see cref="ErrorMessage"/> should be set.
        /// </summary>
        public bool Success = false;

        /// <summary>
        /// True if the operation should be retried. This is more of an internal
        /// detail.
        /// </summary>
        internal bool ShouldRetry = false;

        /// <summary>
        /// Error message if the operation was not successful.
        /// </summary>
        public string ErrorMessage = null;

        /// <summary>
        /// The destination import folder if the relocation result were
        /// successful.
        /// </summary>
        public SVR_ImportFolder ImportFolder = null;

        /// <summary>
        /// The relative path from the <see cref="ImportFolder"/> to where
        /// the file resides.
        /// </summary>
        public string RelativePath = null;

        /// <summary>
        /// Helper to get the full server path if the relative path and import
        /// folder are valid.
        /// </summary>
        /// <returns>The combined path.</returns>
        internal string FullServerPath
            => ImportFolder != null && !string.IsNullOrEmpty(RelativePath) ? Path.Combine(ImportFolder.ImportFolderLocation, RelativePath) : null;
    }

    /// <summary>
    /// Represents a request to automatically rename a file.
    /// </summary>
    public record AutoRenameRequest
    {
        /// <summary>
        /// Indicates whether the result should be a preview of the
        /// relocation.
        /// </summary>
        public bool Preview { get; set; } = false;

        /// <summary>
        /// The name of the renaming script to use. Leave blank to use the
        /// default script.
        /// </summary>
        public string ScriptName = null;

        /// <summary>
        /// Skip the rename operation.
        /// </summary>
        public bool SkipRename = false;
    }

    /// <summary>
    /// Represents a request to automatically move a file.
    /// </summary>
    public record AutoMoveRequest : AutoRenameRequest
    {

        /// <summary>
        /// Indicates whether empty directories should be deleted after
        /// relocating the file.
        /// </summary>
        public bool DeleteEmptyDirectories { get; set; } = true;

        /// <summary>
        /// Skip the move operation.
        /// </summary>
        public bool SkipMove = false;
    }

    /// <summary>
    /// Represents a request to automatically relocate (move and rename) a file.
    /// </summary>
    public record AutoRelocateRequest : AutoMoveRequest { }

    /// <summary>
    /// Represents a request to directly relocate a file.
    /// </summary>
    public record DirectRelocateRequest
    {
        /// <summary>
        /// Indicates whether the result should be a preview of the
        /// relocation.
        /// </summary>
        public bool Preview { get; set; } = false;

        /// <summary>
        /// The import folder where the file should be relocated to.
        /// </summary>
        public SVR_ImportFolder ImportFolder = null;

        /// <summary>
        /// The relative path from the <see cref="ImportFolder"/> where the file
        /// should be relocated to.
        /// </summary>
        public string RelativePath = null;

        /// <summary>
        /// Indicates whether empty directories should be deleted after
        /// relocating the file.
        /// </summary>
        public bool DeleteEmptyDirectories { get; set; } = true;
    }

    #endregion Records & Enums
    #region Methods

    /// <summary>
    /// Relocates a file directly to the specified location based on the given
    /// request.
    /// </summary>
    /// <param name="request">The <see cref="DirectRelocateRequest"/> containing
    /// the details for the relocation operation.</param>
    /// <returns>A <see cref="RelocationResult"/> representing the outcome of
    /// the relocation operation.</returns>
    public RelocationResult DirectlyRelocateFile(DirectRelocateRequest request)
    {
        if (request?.ImportFolder == null || string.IsNullOrWhiteSpace(request.RelativePath))
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = "Invalid request object, import folder, or relative path.",
            };

        // Sanitize relative path and reject paths leading to outside the import folder.
        var fullPath = Path.GetFullPath(Path.Combine(request.ImportFolder.ImportFolderLocation, request.RelativePath));
        if (!fullPath.StartsWith(request.ImportFolder.ImportFolderLocation, StringComparison.OrdinalIgnoreCase))
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = "The provided relative path leads outside the import folder.",
            };
        var newRelativePath = Path.GetRelativePath(request.ImportFolder.ImportFolderLocation, fullPath);

        var oldRelativePath = FilePath;
        var oldFullPath = FullServerPath;
        if (string.IsNullOrWhiteSpace(oldRelativePath) || string.IsNullOrWhiteSpace(oldFullPath))
        {
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = $"Could not find or access the file to move: {VideoLocal_Place_ID}",
            };
        }

        // this can happen due to file locks, so retry in awhile.
        if (!File.Exists(oldRelativePath))
        {
            logger.Warn($"Could not find or access the file to move: \"{oldRelativePath}\"");
            return new()
            {
                Success = false,
                ShouldRetry = true,
                ErrorMessage = $"Could not find or access the file to move: \"{oldFullPath}\"",
            };
        }

        var dropFolder = ImportFolder;
        var newFolderPath = Path.GetDirectoryName(newRelativePath);
        var newFullPath = Path.Combine(request.ImportFolder.ImportFolderLocation, newRelativePath);
        var newFileName = Path.GetFileName(newRelativePath);
        var renamed = !string.Equals(Path.GetFileName(oldRelativePath), newFileName, StringComparison.InvariantCultureIgnoreCase);
        var moved = !string.Equals(Path.GetDirectoryName(oldFullPath), Path.GetDirectoryName(newFullPath), StringComparison.InvariantCultureIgnoreCase);

        // Don't touch files not in a drop source... unless we're requested to.
        if (moved && dropFolder.IsDropSource == 0)
        {
            logger.Trace($"Not moving file as it is NOT in an import folder marked as a drop source: \"{oldFullPath}\"");
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = $"Not moving file as it is NOT in an import folder marked as a drop source: \"{oldFullPath}\"",
            };
        }

        // Last ditch effort to ensure we aren't moving a file unto itself
        if (string.Equals(newFullPath, oldRelativePath, StringComparison.InvariantCultureIgnoreCase))
        {
            logger.Trace($"Resolved to move \"{newFullPath}\" unto itself. NOT MOVING");
            return new()
            {
                Success = true,
                ImportFolder = request.ImportFolder,
                RelativePath = newRelativePath,
            };
        }

        // Only actually do any operations if we're not previewing.
        if (!request.Preview)
        {
            var destFullTree = Path.Combine(request.ImportFolder.ImportFolderLocation, newFolderPath);
            if (!Directory.Exists(destFullTree))
            {
                try
                {
                    Utils.ShokoServer.AddFileWatcherExclusion(destFullTree);
                    Directory.CreateDirectory(destFullTree);
                }
                catch (Exception e)
                {
                    logger.Error(e);
                    return new()
                    {
                        Success = false,
                        ShouldRetry = false,
                        ErrorMessage = e.Message,
                    };
                }
                finally
                {
                    Utils.ShokoServer.RemoveFileWatcherExclusion(destFullTree);
                }
            }
        }

        var sourceFile = new FileInfo(oldRelativePath);
        if (File.Exists(newFullPath))
        {
            // A file with the same name exists at the destination.
            // Handle duplicate files, a duplicate file record won't exist yet,
            // so we'll check the old fashioned way.
            logger.Trace("A file already exists at the new location, checking it for duplicate");
            var destVideoLocalPlace = RepoFactory.VideoLocalPlace.GetByFilePathAndImportFolderID(newRelativePath,
                request.ImportFolder.ImportFolderID);
            var destVideoLocal = destVideoLocalPlace?.VideoLocal;
            if (destVideoLocal == null)
            {
                logger.Warn("The existing file at the new location does not have a VideoLocal. Not moving.");
                return new()
                {
                    Success = false,
                    ShouldRetry = false,
                    ErrorMessage = "The existing file at the new location does not have a VideoLocal. Not moving.",
                };
            }

            if (destVideoLocal.Hash == VideoLocal.Hash)
            {
                logger.Debug($"Not moving file as it already exists at the new location, deleting source file instead: \"{oldFullPath}\" --- \"{newFullPath}\"");

                if (!request.Preview)
                {
                    // if the file already exists, we can just delete the source file instead
                    // this is safer than deleting and moving
                    try
                    {
                        sourceFile.Delete();
                    }
                    catch (Exception e)
                    {
                        logger.Warn($"Unable to DELETE file: \"{FullServerPath}\" error {e}");
                        RemoveRecord(false);

                        if (request.DeleteEmptyDirectories)
                            RecursiveDeleteEmptyDirectories(dropFolder?.ImportFolderLocation, true);
                        return new()
                        {
                            Success = false,
                            ShouldRetry = false,
                            ErrorMessage = $"Unable to DELETE file: \"{FullServerPath}\" error {e}",
                        };
                    }
                }
            }

            // Not a dupe, don't delete it
            logger.Trace("A file already exists at the new location, checking it for version and group");
            var destinationExistingAniDBFile = destVideoLocal.GetAniDBFile();
            if (destinationExistingAniDBFile == null)
            {
                logger.Warn("The existing file at the new location does not have AniDB info. Not moving.");
                return new()
                {
                    Success = false,
                    ShouldRetry = false,
                    ErrorMessage = "The existing file at the new location does not have AniDB info. Not moving.",
                };
            }

            var aniDBFile = VideoLocal.GetAniDBFile();
            if (aniDBFile == null)
            {
                logger.Warn("The file does not have AniDB info. Not moving.");
                return new()
                {
                    Success = false,
                    ShouldRetry = false,
                    ErrorMessage = "The file does not have AniDB info. Not moving.",
                };
            }

            if (destinationExistingAniDBFile.Anime_GroupName == aniDBFile.Anime_GroupName &&
                destinationExistingAniDBFile.FileVersion < aniDBFile.FileVersion)
            {
                // This is a V2 replacing a V1 with the same name.
                // Normally we'd let the Multiple Files Utility handle it, but let's just delete the V1
                logger.Info("The existing file is a V1 from the same group. Replacing it.");
                if (!request.Preview)
                {
                    // Delete the destination
                    destVideoLocalPlace.RemoveRecordAndDeletePhysicalFile();

                    // Move
                    Utils.ShokoServer.AddFileWatcherExclusion(newFullPath);
                    logger.Info($"Moving file from \"{oldFullPath}\" to \"{newFullPath}\"");
                    try
                    {
                        sourceFile.MoveTo(newFullPath);
                    }
                    catch (Exception e)
                    {
                        logger.Error($"Unable to MOVE file: \"{oldFullPath}\" to \"{newFullPath}\" error {e}");
                        Utils.ShokoServer.RemoveFileWatcherExclusion(newFullPath);
                        return new()
                        {
                            Success = false,
                            ShouldRetry = true,
                            ErrorMessage = $"Unable to MOVE file: \"{oldFullPath}\" to \"{newFullPath}\" error {e}",
                        };
                    }

                    ImportFolderID = request.ImportFolder.ImportFolderID;
                    FilePath = newRelativePath;
                    RepoFactory.VideoLocalPlace.Save(this);

                    if (request.DeleteEmptyDirectories)
                        RecursiveDeleteEmptyDirectories(dropFolder?.ImportFolderLocation, true);
                }
            }
        }
        else if (!request.Preview)
        {
            Utils.ShokoServer.AddFileWatcherExclusion(newFullPath);
            logger.Info($"Moving file from \"{oldFullPath}\" to \"{newFullPath}\"");
            try
            {
                sourceFile.MoveTo(newFullPath);
            }
            catch (Exception e)
            {
                logger.Error($"Unable to MOVE file: \"{oldFullPath}\" to \"{newFullPath}\" error {e}");
                Utils.ShokoServer.RemoveFileWatcherExclusion(newFullPath);
                return new()
                {
                    Success = false,
                    ShouldRetry = true,
                    ErrorMessage = $"Unable to MOVE file: \"{oldFullPath}\" to \"{newFullPath}\" error {e}",
                };
            }

            ImportFolderID = request.ImportFolder.ImportFolderID;
            FilePath = newRelativePath;
            RepoFactory.VideoLocalPlace.Save(this);

            if (request.DeleteEmptyDirectories)
                RecursiveDeleteEmptyDirectories(dropFolder?.ImportFolderLocation, true);
        }

        if (!request.Preview)
        {
            if (renamed)
            {
                // Add a new lookup entry.
                var filenameHash = RepoFactory.FileNameHash.GetByHash(VideoLocal.Hash);
                if (!filenameHash.Any(a => a.FileName.Equals(newFileName)))
                {
                    var file = VideoLocal;
                    var fnhash = new FileNameHash
                    {
                        DateTimeUpdated = DateTime.Now,
                        FileName = newFileName,
                        FileSize = file.FileSize,
                        Hash = file.Hash
                    };
                    RepoFactory.FileNameHash.Save(fnhash);
                }
            }

            // Move the external subtitles.
            MoveExternalSubtitles(newFullPath, oldFullPath);

            // Fire off the moved/renamed event depending on what was done.
            if (renamed && !moved)
                ShokoEventHandler.Instance.OnFileRenamed(request.ImportFolder, Path.GetFileName(oldRelativePath), newFileName, this);
            else
                ShokoEventHandler.Instance.OnFileMoved(dropFolder, request.ImportFolder, oldRelativePath, newRelativePath, this);
        }

        return new()
        {
            Success = true,
            ShouldRetry = false,
            ImportFolder = request.ImportFolder,
            RelativePath = newRelativePath,
        };
    }

    /// <summary>
    /// Automatically relocates a file using the specified relocation request or
    /// default settings.
    /// </summary>
    /// <param name="request">The <see cref="AutoRelocateRequest"/> containing
    /// the details for the relocation operation, or null for default settings.</param>
    /// <returns>A <see cref="RelocationResult"/> representing the outcome of
    /// the relocation operation.</returns>
    public RelocationResult AutoRelocateFile(AutoRelocateRequest request = null)
    {
        // Allows calling the method without any parameters.
        request ??= new();

        if (!string.IsNullOrEmpty(request.ScriptName) && string.Equals(request.ScriptName, Shoko.Models.Constants.Renamer.TempFileName))
        {
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = "Do not attempt to use a temp file to rename or move.",
            };
        }

        // Make sure the import folder is reachable.
        var dropFolder = ImportFolder;
        if (dropFolder == null)
        {
            logger.Warn($"Unable to find import folder with id {ImportFolderID}");
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = $"Unable to find import folder with id {ImportFolderID}",
            };
        }

        // Make sure the path is resolvable.
        var oldFullPath = Path.Combine(dropFolder.ImportFolderLocation, FilePath);
        if (string.IsNullOrWhiteSpace(FilePath) || string.IsNullOrWhiteSpace(oldFullPath))
        {
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = $"Could not find or access the file to move: {VideoLocal_Place_ID}",
            };
        }

        var settings = Utils.SettingsProvider.GetSettings();
        RelocationResult renameResult;
        RelocationResult moveResult;
        if (settings.Import.RenameThenMove)
        {
            // Try a maximum of 4 times to rename, and after that we bail.
            renameResult = RenameFile(request);
            if (!renameResult.Success && renameResult.ShouldRetry)
            {
                Thread.Sleep((int)DELAY_IN_USE.FIRST);
                renameResult = RenameFile(request);
                if (!renameResult.Success && renameResult.ShouldRetry)
                {
                    Thread.Sleep((int)DELAY_IN_USE.SECOND);
                    renameResult = RenameFile(request);
                    if (!renameResult.Success && renameResult.ShouldRetry)
                    {
                        Thread.Sleep((int)DELAY_IN_USE.THIRD);
                        renameResult = RenameFile(request);
                    }
                }
            }
            if (!renameResult.Success)
                return renameResult;

            // Same as above, just for moving instead.
            moveResult = MoveFile(request);
            if (!moveResult.Success && moveResult.ShouldRetry)
            {
                Thread.Sleep((int)DELAY_IN_USE.FIRST);
                moveResult = MoveFile(request);
                if (!moveResult.Success && moveResult.ShouldRetry)
                {
                    Thread.Sleep((int)DELAY_IN_USE.SECOND);
                    moveResult = MoveFile(request);
                    if (!moveResult.Success && moveResult.ShouldRetry)
                    {
                        Thread.Sleep((int)DELAY_IN_USE.THIRD);
                        moveResult = MoveFile(request);
                    }
                }
            }
            if (!moveResult.Success)
                return moveResult;
        }
        else
        {
            moveResult = MoveFile(request);
            if (!moveResult.Success && moveResult.ShouldRetry)
            {
                Thread.Sleep((int)DELAY_IN_USE.FIRST);
                moveResult = MoveFile(request);
                if (!moveResult.Success && moveResult.ShouldRetry)
                {
                    Thread.Sleep((int)DELAY_IN_USE.SECOND);
                    moveResult = MoveFile(request);
                    if (!moveResult.Success && moveResult.ShouldRetry)
                    {
                        Thread.Sleep((int)DELAY_IN_USE.THIRD);
                        moveResult = MoveFile(request);
                    }
                }
            }
            if (!moveResult.Success)
                return moveResult;

            renameResult = RenameFile(request);
            if (!renameResult.Success && renameResult.ShouldRetry)
            {
                Thread.Sleep((int)DELAY_IN_USE.FIRST);
                renameResult = RenameFile(request);
                if (!renameResult.Success && renameResult.ShouldRetry)
                {
                    Thread.Sleep((int)DELAY_IN_USE.SECOND);
                    renameResult = RenameFile(request);
                    if (!renameResult.Success && renameResult.ShouldRetry)
                    {
                        Thread.Sleep((int)DELAY_IN_USE.THIRD);
                        renameResult = RenameFile(request);
                    }
                }
            }
            if (!renameResult.Success)
                return renameResult;
        }

        // Set the linux permissions now if we're not previewing the result.
        if (!request.Preview)
        {
            Utils.ShokoServer.AddFileWatcherExclusion(FullServerPath);
            try
            {
                LinuxFS.SetLinuxPermissions(FullServerPath, settings.Linux.UID,
                    settings.Linux.GID, settings.Linux.Permission);
            }
            catch (InvalidOperationException e)
            {
                logger.Error(e, $"Unable to set permissions ({settings.Linux.UID}:{settings.Linux.GID} {settings.Linux.Permission}) on file {FileName}: Access Denied");
            }
            catch (Exception e)
            {
                logger.Error(e, "Error setting Linux Permissions: {0}", e);
            }
            Utils.ShokoServer.RemoveFileWatcherExclusion(FullServerPath);
        }

        var correctFileName = !string.IsNullOrEmpty(renameResult.RelativePath) ? Path.GetFileName(renameResult.RelativePath) : null;
        var correctFolder = !string.IsNullOrEmpty(moveResult.RelativePath) ? Path.GetDirectoryName(moveResult.RelativePath) : null;
        var combinedPath = !string.IsNullOrEmpty(correctFolder) && !string.IsNullOrEmpty(correctFileName) ? Path.Combine(correctFolder, correctFileName) : null;
        return new()
        {
            Success = renameResult.Success && moveResult.Success,
            ShouldRetry = renameResult.ShouldRetry || moveResult.ShouldRetry,
            ImportFolder = moveResult.ImportFolder,
            RelativePath = combinedPath,
        };
    }

    /// <summary>
    /// Renames a file using the specified rename request.
    /// </summary>
    /// <param name="request">The <see cref="AutoRenameRequest"/> containing the
    /// details for the rename operation.</param>
    /// <returns>A <see cref="RelocationResult"/> representing the outcome of
    /// the rename operation.</returns>
    private RelocationResult RenameFile(AutoRenameRequest request)
    {
        // Just return the existing values if we're going to skip the operation.
        if (request.SkipRename)
            return new()
            {
                Success = true,
                ShouldRetry = false,
                ImportFolder = ImportFolder,
                RelativePath = FileName,
            };

        var newFileName = RenameFileHelper.GetFilename(this, request.ScriptName);
        if (string.IsNullOrWhiteSpace(newFileName))
        {
            logger.Error($"Error: The renamer returned a null or empty name for: \"{FullServerPath}\"");
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = "The file renamer returned a null or empty value",
            };
        }

        if (newFileName.StartsWith("*Error:"))
        {
            logger.Error($"Error: The renamer returned an error on file: \"{FullServerPath}\"\n            {newFileName}");
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = newFileName.Substring(7).TrimStart(),
            };
        }

        var newFullPath = Path.Combine(Path.GetDirectoryName(FullServerPath), newFileName);
        var newRelativePath = Path.GetRelativePath(ImportFolder.ImportFolderLocation, newFullPath);
        return DirectlyRelocateFile(new()
        {
            Preview = request.Preview,
            DeleteEmptyDirectories = false,
            ImportFolder = ImportFolder,
            RelativePath = newRelativePath,
        });
    }

    /// <summary>
    /// Moves a file using the specified move request.
    /// </summary>
    /// <param name="request">The <see cref="AutoMoveRequest"/> containing the
    /// details for the move operation.</param>
    /// <returns>A <see cref="RelocationResult"/> representing the outcome of
    /// the move operation.</returns>
    private RelocationResult MoveFile(AutoMoveRequest request)
    {
        // Just return the existing values if we're going to skip the operation.
        if (request.SkipMove)
            return new()
            {
                Success = true,
                ShouldRetry = false,
                ImportFolder = ImportFolder,
                RelativePath = FileName,
            };

        // Find the new destination.
        var (destImpl, newFolderPath) = RenameFileHelper.GetDestination(this, request.ScriptName);

        // Ensure the new folder path is not null.
        newFolderPath ??= "";

        // Check if we have an import folder selected.
        if (!(destImpl is SVR_ImportFolder importFolder))
        {
            logger.Warn($"Could not find a valid destination: \"{FullServerPath}\"");
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = !string.IsNullOrWhiteSpace(newFolderPath) ? (
                    newFolderPath.StartsWith("*Error:", StringComparison.InvariantCultureIgnoreCase) ? (
                        newFolderPath.Substring(7).TrimStart()
                    ) : (
                        newFolderPath
                    )
                ) : (
                    $"Could not find a valid destination: \"{FullServerPath}"
                ),
            };
        }

        // Check the path for errors, even if an import folder is selected.
        if (newFolderPath.StartsWith("*Error:", StringComparison.InvariantCultureIgnoreCase))
        {
            logger.Warn($"Could not find a valid destination: \"{FullServerPath}\"");
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = newFolderPath.Substring(7).TrimStart(),
            };
        }

        // Relocate the file to the new destination.
        return DirectlyRelocateFile(new()
        {
            Preview = request.Preview,
            DeleteEmptyDirectories = request.DeleteEmptyDirectories,
            ImportFolder = importFolder,
            RelativePath = Path.Combine(newFolderPath, FileName),
        });
    }

    #endregion Methods
    #region Move On Import

    public void RenameAndMoveAsRequired()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        if (!settings.Import.RenameOnImport)
            logger.Trace($"Skipping rename of \"{FullServerPath}\" as rename on import is disabled");
        if (!!settings.Import.MoveOnImport)
            logger.Trace($"Skipping move of \"{this.FullServerPath}\" as move on import is disabled");

        AutoRelocateFile(new AutoRelocateRequest()
        {
            SkipRename = !settings.Import.RenameOnImport,
            SkipMove = !settings.Import.MoveOnImport,
        });
    }

    #endregion Move On Import
    #region Helpers

    private static void MoveExternalSubtitles(string newFullPath, string srcFullPath)
    {
        try
        {
            var srcParent = Path.GetDirectoryName(srcFullPath);
            var newParent = Path.GetDirectoryName(newFullPath);
            if (string.IsNullOrEmpty(newParent) || string.IsNullOrEmpty(srcParent))
                return;

            var textStreams = SubtitleHelper.GetSubtitleStreams(srcFullPath);
            // move any subtitle files
            foreach (var subtitleFile in textStreams)
            {
                if (string.IsNullOrEmpty(subtitleFile.Filename))
                    continue;

                var subPath = Path.Combine(srcParent, subtitleFile.Filename);
                if (!File.Exists(subPath))
                {
                    logger.Error($"Unable to rename external subtitle file \"{subtitleFile.Filename}\". Cannot access the file");
                    continue;
                }

                var subFile = new FileInfo(subPath);
                var newSubPath = Path.Combine(newParent, subFile.Name);
                if (File.Exists(newSubPath))
                {
                    try
                    {
                        File.Delete(newSubPath);
                    }
                    catch (Exception e)
                    {
                        logger.Warn($"Unable to DELETE file: \"{subtitleFile}\" error {e}");
                    }
                }

                try
                {
                    subFile.MoveTo(newSubPath);
                }
                catch (Exception e)
                {
                    logger.Error($"Unable to MOVE file: \"{subtitleFile}\" to \"{newSubPath}\" error {e}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.Message);
        }
    }

    private static void DeleteExternalSubtitles(string srcFullPath)
    {
        try
        {
            var textStreams = SubtitleHelper.GetSubtitleStreams(srcFullPath);
            // move any subtitle files
            foreach (var subtitleFile in textStreams)
            {
                if (string.IsNullOrEmpty(subtitleFile.Filename)) continue;

                var srcParent = Path.GetDirectoryName(srcFullPath);
                if (string.IsNullOrEmpty(srcParent)) continue;

                var subPath = Path.Combine(srcParent, subtitleFile.Filename);
                if (!File.Exists(subPath)) continue;

                try
                {
                    File.Delete(subPath);
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Unable to delete file: \"{subtitleFile}\"");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }
    }

    private void RecursiveDeleteEmptyDirectories(string dir, bool importfolder)
    {
        try
        {
            if (string.IsNullOrEmpty(dir))
            {
                return;
            }

            if (!Directory.Exists(dir))
            {
                return;
            }

            if (IsDirectoryEmpty(dir))
            {
                if (importfolder)
                {
                    return;
                }

                try
                {
                    Directory.Delete(dir);
                }
                catch (Exception ex)
                {
                    if (ex is DirectoryNotFoundException || ex is FileNotFoundException)
                    {
                        return;
                    }

                    logger.Warn("Unable to DELETE directory: {0} Error: {1}", dir,
                        ex);
                }

                return;
            }

            // If it has folder, recurse
            foreach (var d in Directory.EnumerateDirectories(dir))
            {
                if (Utils.SettingsProvider.GetSettings().Import.Exclude.Any(s => Regex.IsMatch(Path.GetDirectoryName(d) ?? string.Empty, s))) continue;
                RecursiveDeleteEmptyDirectories(d, false);
            }
        }
        catch (Exception e)
        {
            if (e is FileNotFoundException || e is DirectoryNotFoundException)
            {
                return;
            }

            logger.Error($"There was an error removing the empty directory: {dir}\r\n{e}");
        }
    }

    public bool IsDirectoryEmpty(string path)
    {
        try
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }
        catch
        {
            return false;
        }
    }

    #endregion Helpers
    #endregion Relocation (Move & Rename)
    #region Remove Record

    public void RemoveRecordAndDeletePhysicalFile(bool deleteFolder = true)
    {
        logger.Info("Deleting video local place record and file: {0}",
            FullServerPath ?? VideoLocal_Place_ID.ToString());

        if (!File.Exists(FullServerPath))
        {
            logger.Info($"Unable to find file. Removing Record: {FullServerPath ?? FilePath}");
            RemoveRecord();
            return;
        }

        try
        {
            File.Delete(FullServerPath);
            DeleteExternalSubtitles(FullServerPath);
        }
        // Just continue if the file doesn't exist.
        catch (FileNotFoundException) { }
        catch (Exception ex)
        {
            logger.Error($"Unable to delete file '{FullServerPath}': {ex}");
            throw;
        }

        if (deleteFolder)
            RecursiveDeleteEmptyDirectories(ImportFolder?.ImportFolderLocation, true);
        RemoveRecord();
    }

    public void RemoveAndDeleteFileWithOpenTransaction(ISession session, HashSet<SVR_AnimeSeries> seriesToUpdate)
    {
        logger.Info("Deleting video local place record and file: {0}",
            FullServerPath ?? VideoLocal_Place_ID.ToString());


        if (!File.Exists(FullServerPath))
        {
            logger.Info($"Unable to find file. Removing Record: {FullServerPath}");
            RemoveRecordWithOpenTransaction(session, seriesToUpdate);
            return;
        }

        try
        {
            File.Delete(FullServerPath);
            DeleteExternalSubtitles(FullServerPath);
        }
        // Just continue if the file doesn't exist.
        catch (FileNotFoundException) { }
        catch (Exception ex)
        {
            logger.Error($"Unable to delete file '{FullServerPath}': {ex}");
            return;
        }

        RecursiveDeleteEmptyDirectories(ImportFolder?.ImportFolderLocation, true);
        RemoveRecordWithOpenTransaction(session, seriesToUpdate);
    }

    public void RemoveRecord(bool updateMyListStatus = true)
    {
        logger.Info("Removing VideoLocal_Place record for: {0}", FullServerPath ?? VideoLocal_Place_ID.ToString());
        var seriesToUpdate = new List<SVR_AnimeSeries>();
        var v = VideoLocal;
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            if (v?.Places?.Count <= 1)
            {
                if (updateMyListStatus)
                {
                    if (RepoFactory.AniDB_File.GetByHash(v.Hash) == null)
                    {
                        var xrefs = RepoFactory.CrossRef_File_Episode.GetByHash(v.Hash);
                        foreach (var xref in xrefs)
                        {
                            var ep = RepoFactory.AniDB_Episode.GetByEpisodeID(xref.EpisodeID);
                            if (ep == null) continue;

                            commandFactory.CreateAndSave<CommandRequest_DeleteFileFromMyList>(
                                c =>
                                {
                                    c.AnimeID = xref.AnimeID;
                                    c.EpisodeType = ep.GetEpisodeTypeEnum();
                                    c.EpisodeNumber = ep.EpisodeNumber;
                                }
                            );
                        }
                    }
                    else
                    {
                        commandFactory.CreateAndSave<CommandRequest_DeleteFileFromMyList>(
                            c =>
                            {
                                c.Hash = v.Hash;
                                c.FileSize = v.FileSize;
                            }
                        );
                    }
                }

                try
                {
                    ShokoEventHandler.Instance.OnFileDeleted(ImportFolder, this);
                }
                catch
                {
                    // ignore
                }

                BaseRepository.Lock(session, s =>
                {
                    using var transaction = s.BeginTransaction();
                    RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(s, this);

                    seriesToUpdate.AddRange(v.GetAnimeEpisodes().DistinctBy(a => a.AnimeSeriesID)
                        .Select(a => a.GetAnimeSeries()));
                    RepoFactory.VideoLocal.DeleteWithOpenTransaction(s, v);

                    transaction.Commit();
                });
            }
            else
            {
                try
                {
                    ShokoEventHandler.Instance.OnFileDeleted(ImportFolder, this);
                }
                catch
                {
                    // ignore
                }

                BaseRepository.Lock(session, s =>
                {
                    using var transaction = s.BeginTransaction();
                    RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(s, this);
                    transaction.Commit();
                });
            }
        }

        foreach (var ser in seriesToUpdate)
        {
            ser?.QueueUpdateStats();
        }
    }


    public void RemoveRecordWithOpenTransaction(ISession session, ICollection<SVR_AnimeSeries> seriesToUpdate,
        bool updateMyListStatus = true)
    {
        logger.Info("Removing VideoLocal_Place record for: {0}", FullServerPath ?? VideoLocal_Place_ID.ToString());
        var v = VideoLocal;
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();

        if (v?.Places?.Count <= 1)
        {
            if (updateMyListStatus)
            {
                if (RepoFactory.AniDB_File.GetByHash(v.Hash) == null)
                {
                    var xrefs = RepoFactory.CrossRef_File_Episode.GetByHash(v.Hash);
                    foreach (var xref in xrefs)
                    {
                        var ep = RepoFactory.AniDB_Episode.GetByEpisodeID(xref.EpisodeID);
                        if (ep == null)
                        {
                            continue;
                        }

                        commandFactory.CreateAndSave<CommandRequest_DeleteFileFromMyList>(c =>
                        {
                            c.AnimeID = xref.AnimeID;
                            c.EpisodeType = ep.GetEpisodeTypeEnum();
                            c.EpisodeNumber = ep.EpisodeNumber;
                        });
                    }
                }
                else
                {
                    commandFactory.CreateAndSave<CommandRequest_DeleteFileFromMyList>(
                        c =>
                        {
                            c.Hash = v.Hash;
                            c.FileSize = v.FileSize;
                        }
                    );
                }
            }

            var eps = v?.GetAnimeEpisodes()?.Where(a => a != null).ToList();
            eps?.DistinctBy(a => a.AnimeSeriesID).Select(a => a.GetAnimeSeries()).ToList().ForEach(seriesToUpdate.Add);

            try
            {
                ShokoEventHandler.Instance.OnFileDeleted(ImportFolder, this);
            }
            catch
            {
                // ignore
            }

            BaseRepository.Lock(() =>
            {
                using var transaction = session.BeginTransaction();
                RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, this);
                RepoFactory.VideoLocal.DeleteWithOpenTransaction(session, v);

                transaction.Commit();
            });
        }
        else
        {
            try
            {
                ShokoEventHandler.Instance.OnFileDeleted(ImportFolder, this);
            }
            catch
            {
                // ignore
            }

            BaseRepository.Lock(() =>
            {
                using var transaction = session.BeginTransaction();
                RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, this);
                transaction.Commit();
            });
        }
    }

    #endregion Remove Record
    #region IVideoFile Implementation

    int IVideoFile.VideoFileID
        => VideoLocalID;

    string IVideoFile.Filename
        => Path.GetFileName(FilePath);

    string IVideoFile.FilePath
        => FullServerPath;

    long IVideoFile.FileSize
        => VideoLocal?.FileSize ?? 0;

    IAniDBFile IVideoFile.AniDBFileInfo
        => VideoLocal?.GetAniDBFile();

    IHashes IVideoFile.Hashes => VideoLocal == null
        ? null
        : new VideoHashes
        {
            CRC = VideoLocal.CRC32,
            MD5 = VideoLocal.MD5,
            ED2K = VideoLocal.Hash,
            SHA1 = VideoLocal.SHA1,
        };

    IMediaContainer IVideoFile.MediaInfo
        => VideoLocal?.Media;

    private class VideoHashes : IHashes
    {
        public string CRC { get; set; }
        public string MD5 { get; set; }
        public string ED2K { get; set; }
        public string SHA1 { get; set; }
    }

    #endregion IVideoFile Implementation
}
