using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Databases;

/// <summary>
/// Moves cached image files from the folders and names older versions kept
/// them under to <c>&lt;images&gt;/&lt;value&gt;/&lt;id[..2]&gt;/&lt;id&gt;&lt;ext&gt;</c>,
/// where the value is the source's <see cref="MetadataSource.Value"/> and the
/// ID is the one hashed from it.
/// </summary>
/// <param name="imagesPath">The image cache folder.</param>
/// <param name="caseInsensitive">
/// Whether the file system ignores case, or <c>null</c> to find out.
/// </param>
internal sealed class ImageCacheMigrator(string imagesPath, bool? caseInsensitive = null)
{
    #region Fields

    private readonly bool _caseInsensitive = caseInsensitive ?? IsCaseInsensitive(imagesPath);

    private StringComparer NameComparer => _caseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    // The files of each folder read so far, by the folder's full path, kept
    // up to date with the moves and deletes made here.
    private Dictionary<string, DirectoryIndex> DirectoryIndexes => field ??= new(NameComparer);

    private HashSet<string> _folderNames = ReadFolderNames(imagesPath);

    /// <summary>
    /// The image cache folder.
    /// </summary>
    public string ImagesPath { get; } = imagesPath;

    #endregion

    #region Paths

    /// <summary>
    /// Gets the folder names, other than its value, that older versions kept
    /// a source's images in: the old enum spelling, as
    /// <see cref="DatabaseFixes.GetOldSourceSpelling"/> gives it.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The old folder names, which may be empty.</returns>
    public static IReadOnlyList<string> GetOldFolderNames(MetadataSource source)
        => DatabaseFixes.GetOldSourceSpelling(source.Value) is { } oldName && !string.Equals(oldName, source.Value, StringComparison.Ordinal)
            ? [oldName]
            : [];

    /// <summary>
    /// Gets the path an image's file belongs at.
    /// </summary>
    /// <param name="value">The source's value.</param>
    /// <param name="id">The image ID, as 32 hex digits.</param>
    /// <param name="extension">The extension, with its dot.</param>
    /// <returns>The full path.</returns>
    public string GetTargetPath(string value, string id, string extension)
        => Path.Join(ImagesPath, value, id[..2], id + extension);

    #endregion

    #region Folders

    /// <summary>
    /// Renames the first old folder of a source to its value when no folder
    /// has that exact name yet. A case-only rename goes through a temporary
    /// folder (finishing one an interrupted run left). The folders are read
    /// again even when the rename fails, so a folder left at the temporary
    /// name is still searched by <see cref="FindFiles(string, IReadOnlyList{string}, IReadOnlyList{string})"/>.
    /// </summary>
    /// <param name="value">The source's value.</param>
    /// <param name="oldNames">The source's old folder names.</param>
    /// <returns><c>true</c> if a folder was renamed.</returns>
    /// <exception cref="IOException">Thrown when the folder can not be renamed.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when renaming the folder is not allowed.</exception>
    public bool RenameOldFolder(string value, IReadOnlyList<string> oldNames)
    {
        if (_folderNames.Contains(value))
            return false;

        try
        {
            if (_folderNames.Contains(GetTemporaryName(value)))
                Directory.Move(Path.Join(ImagesPath, GetTemporaryName(value)), Path.Join(ImagesPath, value));
            else if (oldNames.FirstOrDefault(_folderNames.Contains) is { } oldName)
                MoveDirectory(oldName, value);
            else
                return false;
        }
        finally
        {
            _folderNames = ReadFolderNames(ImagesPath);
            DirectoryIndexes.Clear();
        }

        return true;
    }

    /// <summary>
    /// Deletes the old folders, and the temporary folders of interrupted
    /// renames, that hold no files anymore. A folder that is some source's
    /// value is never deleted.
    /// </summary>
    /// <param name="oldNames">The old folder names to check.</param>
    /// <param name="values">Every source value that may have a folder.</param>
    /// <returns>The number of folders deleted.</returns>
    public int RemoveEmptyOldFolders(IEnumerable<string> oldNames, IReadOnlySet<string> values)
    {
        var removed = 0;
        foreach (var oldName in oldNames.Concat(values.Select(GetTemporaryName)).Distinct(StringComparer.Ordinal))
        {
            if (values.Contains(oldName) || !_folderNames.Contains(oldName))
                continue;

            var path = Path.Join(ImagesPath, oldName);
            if (Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Any())
                continue;

            Directory.Delete(path, recursive: true);
            removed++;
        }

        _folderNames = ReadFolderNames(ImagesPath);
        DirectoryIndexes.Clear();
        return removed;
    }

    private void MoveDirectory(string oldName, string value)
    {
        var from = Path.Join(ImagesPath, oldName);
        var to = Path.Join(ImagesPath, value);
        if (!string.Equals(oldName, value, StringComparison.OrdinalIgnoreCase))
        {
            Directory.Move(from, to);
            return;
        }

        var temporary = Path.Join(ImagesPath, GetTemporaryName(value));
        Directory.Move(from, temporary);
        try
        {
            Directory.Move(temporary, to);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Put the folder back. If that fails too, the files are found in
            // the temporary folder, and the next run finishes the rename.
            try
            {
                Directory.Move(temporary, from);
            }
            catch (Exception restoreEx) when (restoreEx is IOException or UnauthorizedAccessException)
            {
            }

            throw;
        }
    }

    // The fixed name a case-only rename passes through. A value never holds
    // a dot, so it can not clash with another source's folder.
    private static string GetTemporaryName(string value)
        => value + ".moving";

    // The names as they are on disk. On a case-insensitive file system this
    // tells "AniDB" from "anidb", which Directory.Exists can not.
    private static HashSet<string> ReadFolderNames(string path)
        => Directory.Exists(path)
            ? Directory.EnumerateDirectories(path).Select(Path.GetFileName).OfType<string>().ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

    #endregion

    #region Files

    /// <summary>
    /// Finds every file kept for an image, named after the ID with or without
    /// an extension, in the value folder first, then in a temporary folder left
    /// by an interrupted rename, and then in the old folders.
    /// </summary>
    /// <param name="value">The source's value.</param>
    /// <param name="oldNames">The source's old folder names.</param>
    /// <param name="id">The image ID, as 32 hex digits.</param>
    /// <returns>The full paths of the files found.</returns>
    public IReadOnlyList<string> FindFiles(string value, IReadOnlyList<string> oldNames, string id)
        => FindFiles(value, oldNames, [id]);

    /// <summary>
    /// Finds every file kept for an image under any of the IDs it may have
    /// had, one ID after another in the order given, and for each ID in the
    /// folders <see cref="FindFiles(string, IReadOnlyList{string}, string)"/>
    /// looks in.
    /// </summary>
    /// <param name="value">The source's value.</param>
    /// <param name="oldNames">The source's old folder names.</param>
    /// <param name="ids">The image IDs, as 32 hex digits.</param>
    /// <returns>The full paths of the files found.</returns>
    public IReadOnlyList<string> FindFiles(string value, IReadOnlyList<string> oldNames, IReadOnlyList<string> ids)
    {
        var folders = oldNames.Prepend(GetTemporaryName(value)).Prepend(value)
            .Where(_folderNames.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return ids
            .Distinct(StringComparer.Ordinal)
            .SelectMany(id => folders.SelectMany(folder => GetIndex(Path.Join(ImagesPath, folder, id[..2])).GetFiles(id)))
            .ToList();
    }

    /// <summary>
    /// Checks whether a file exists, from the listing of its folder: read
    /// once per folder, and kept up to date with the moves and deletes made
    /// here. Matches <see cref="File.Exists"/> as long as nothing else
    /// changes the image folder meanwhile.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    /// <returns><c>true</c> if the folder holds a file by that name.</returns>
    public bool FileExists(string path)
        => GetIndex(Path.GetDirectoryName(path)!).Contains(path);

    /// <summary>
    /// Moves one of an image's files to the target path, unless a file is
    /// already there, then deletes the other files as duplicates. Never
    /// overwrites the target.
    /// </summary>
    /// <param name="targetPath">The path the file belongs at.</param>
    /// <param name="files">
    /// The image's files, from <see cref="FindFiles(string, IReadOnlyList{string}, IReadOnlyList{string})"/>.
    /// </param>
    /// <returns>What was done.</returns>
    public ImageMoveResult MoveToTarget(string targetPath, IReadOnlyList<string> files)
    {
        // On a case-insensitive file system, a path that differs from the
        // target only by case is the target itself, so it is never touched.
        var comparison = _caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var others = files.Where(file => !string.Equals(file, targetPath, comparison)).ToList();
        var result = ImageMoveResult.InPlace;

        // The folder listings answer for the file system. Only before
        // deleting duplicates is the target checked on disk as well.
        if (!FileExists(targetPath) || others.Count > 0 && !File.Exists(targetPath))
        {
            var extension = Path.GetExtension(targetPath);
            var source = others.FirstOrDefault(file => string.Equals(Path.GetExtension(file), extension, StringComparison.OrdinalIgnoreCase)
                    && FileExists(file))
                ?? others.FirstOrDefault(file => Path.GetExtension(file).Length is 0 && FileExists(file))
                ?? others.FirstOrDefault(FileExists);
            if (source is null)
                return ImageMoveResult.Missing;

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Move(source, targetPath, overwrite: false);
            UpdateIndex(source, exists: false);
            UpdateIndex(targetPath, exists: true);
            others.Remove(source);
            result = ImageMoveResult.Moved;
        }

        foreach (var file in others.Where(FileExists))
        {
            File.Delete(file);
            UpdateIndex(file, exists: false);
        }

        return result;
    }

    private static bool IsCaseInsensitive(string path)
    {
        if (!Directory.Exists(path))
            return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

        var name = ".case-probe-" + Guid.NewGuid().ToString("N");
        var probe = Path.Join(path, name);
        try
        {
            File.WriteAllBytes(probe, []);
            try
            {
                return File.Exists(Path.Join(path, name.ToUpperInvariant()));
            }
            finally
            {
                File.Delete(probe);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();
        }
    }

    private DirectoryIndex GetIndex(string directory)
    {
        var key = GetDirectoryKey(directory);
        if (DirectoryIndexes.TryGetValue(key, out var index))
            return index;

        index = new DirectoryIndex(NameComparer);
        if (Directory.Exists(directory))
        {
            foreach (var file in Directory.EnumerateFiles(directory))
                index.Add(file);
        }

        DirectoryIndexes[key] = index;
        return index;
    }

    // Records a move or delete made here in the listing of the file's folder,
    // if that folder was read. A folder read later lists the file itself.
    private void UpdateIndex(string path, bool exists)
    {
        if (!DirectoryIndexes.TryGetValue(GetDirectoryKey(Path.GetDirectoryName(path)!), out var index))
            return;

        if (exists)
            index.Add(path);
        else
            index.Remove(path);
    }

    // One key per folder however its path is spelled.
    private static string GetDirectoryKey(string directory)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));

    #endregion

    #region Nested Types

    /// <summary>
    /// The files of one folder, by name and by the image ID they are named
    /// after: the part of the name before the first dot.
    /// </summary>
    /// <param name="nameComparer">Compares file names the way the file system does.</param>
    private sealed class DirectoryIndex(StringComparer nameComparer)
    {
        private readonly HashSet<string> _names = new(nameComparer);

        private readonly Dictionary<string, List<string>> _filesByID = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets the files named after an image ID.
        /// </summary>
        /// <param name="id">The image ID, as 32 hex digits.</param>
        /// <returns>The full paths of the files.</returns>
        public IReadOnlyList<string> GetFiles(string id)
            => _filesByID.TryGetValue(id, out var files) ? files : [];

        /// <summary>
        /// Checks whether the folder holds a file by the name of a path.
        /// </summary>
        /// <param name="path">The full path of the file.</param>
        /// <returns><c>true</c> if the folder holds the file.</returns>
        public bool Contains(string path)
            => _names.Contains(Path.GetFileName(path));

        /// <summary>
        /// Adds a file to the folder.
        /// </summary>
        /// <param name="path">The full path of the file.</param>
        public void Add(string path)
        {
            if (!_names.Add(Path.GetFileName(path)))
                return;

            var id = GetID(path);
            if (!_filesByID.TryGetValue(id, out var files))
                _filesByID[id] = files = [];
            files.Add(path);
        }

        /// <summary>
        /// Removes a file from the folder.
        /// </summary>
        /// <param name="path">The full path of the file.</param>
        public void Remove(string path)
        {
            var name = Path.GetFileName(path);
            if (!_names.Remove(name))
                return;

            if (_filesByID.TryGetValue(GetID(path), out var files))
                files.RemoveAll(file => _names.Comparer.Equals(Path.GetFileName(file), name));
        }

        private static string GetID(string path)
            => Path.GetFileName(path).Split('.', 2)[0];
    }

    #endregion
}

/// <summary>
/// What <see cref="ImageCacheMigrator.MoveToTarget"/> did with an image.
/// </summary>
internal enum ImageMoveResult
{
    /// <summary>
    /// The file was already at the target.
    /// </summary>
    InPlace,

    /// <summary>
    /// A file was moved to the target.
    /// </summary>
    Moved,

    /// <summary>
    /// No file was found.
    /// </summary>
    Missing,
}
