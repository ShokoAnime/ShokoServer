using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shoko.Abstractions.FileSystem;
using Shoko.Abstractions.FileSystem.Services;
using Shoko.Abstractions.Video;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Services;

/// <summary>
///   Groups a video's locations into its file groups: from what is stored,
///   to read them, and from the disk, to store them.
/// </summary>
internal static class VideoFileGrouper
{
    /// <summary>
    ///   What a location is on disk right now.
    /// </summary>
    /// <param name="Exists">Whether anything is at its path, a link whose target is gone included.</param>
    /// <param name="Identity">The identity of the file it is, through links; <c>null</c> when that cannot be read.</param>
    /// <param name="IsSymbolicLink">Whether it is a symbolic link.</param>
    /// <param name="SymbolicLinkTarget">The absolute path the link leads to, through every link on the way.</param>
    internal readonly record struct OnDisk(bool Exists, StatIdentity? Identity, bool IsSymbolicLink, string? SymbolicLinkTarget);

    /// <summary>
    ///   Sorts locations into groups by their stored
    ///   <see cref="VideoLocal_Place.PrimaryLocationID"/>, reading nothing
    ///   from disk. A location whose primary is not among them, such as a new
    ///   one not saved yet, is a group of its own.
    /// </summary>
    /// <param name="files">The video's locations.</param>
    /// <param name="joinLinks">Whether links join their file's group; otherwise every location is its own group.</param>
    /// <param name="firstAvailableFileID">The location to keep when none is kept, if any.</param>
    /// <param name="ignoreKept">Whether to treat no location as kept.</param>
    /// <returns>The groups, the preferred ones first, then by ID.</returns>
    public static IReadOnlyList<VideoFileGroup> Group(IReadOnlyList<VideoLocal_Place> files, bool joinLinks, int? firstAvailableFileID, bool ignoreKept)
    {
        if (files.Count is 0)
            return [];

        var ids = files.Select(file => file.ID).ToHashSet();
        var groups = files
            .GroupBy(file => joinLinks && file.PrimaryLocationID is not 0 && ids.Contains(file.PrimaryLocationID) ? file.PrimaryLocationID : file.ID)
            .Select(group => (
                ID: group.Key,
                Files: (IReadOnlyList<IVideoFile>)group.OrderBy(file => file.ID == group.Key ? 0 : 1).ThenBy(file => file.ID).ToList(),
                IsKept: !ignoreKept && group.Any(file => file.IsKept)
            ))
            .OrderBy(group => group.ID)
            .ToList();
        var anyKept = groups.Any(group => group.IsKept);
        var firstAvailable = firstAvailableFileID is { } fileID ? groups.FindIndex(group => group.Files.Any(file => file.ID == fileID)) : -1;
        if (firstAvailable < 0)
            firstAvailable = 0;

        return groups
            .Select((group, index) => new VideoFileGroup
            {
                ID = group.ID,
                Files = group.Files,
                IsKept = group.IsKept,
                IsPreferred = anyKept ? group.IsKept : index == firstAvailable,
            })
            .OrderByDescending(group => group.IsPreferred)
            .ThenBy(group => group.ID)
            .ToList();
    }

    /// <summary>
    ///   Reads what a location is on disk: one <c>lstat</c>, one <c>stat</c>
    ///   through it, and for a link the walk to its target.
    /// </summary>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="path">The location's absolute path.</param>
    /// <returns>What it is.</returns>
    public static OnDisk Read(IFileSystem fileSystem, string? path)
    {
        if (string.IsNullOrEmpty(path))
            return default;

        try
        {
            var own = fileSystem.GetStat(path, followSymbolicLinks: false);
            if (own is null)
                return default;

            var isLink = own is SymbolicLinkStat;
            string? target = null;
            if (isLink)
            {
                try
                {
                    target = fileSystem.ResolveSymbolicLink(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A loop or an unreadable step: a link all the same, with no target to show.
                }
            }

            var identity = isLink ? fileSystem.GetFileStat(path)?.Identity : (own as FileStat)?.Identity;
            return new(true, identity, isLink, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return default;
        }
    }

    /// <summary>
    ///   Works out from the disk which locations are one group and which of
    ///   each is the primary: the lowest ID among those that are not
    ///   symbolic links, or the lowest ID when all are, so a link added
    ///   before the file it points to hands over to the file once it is
    ///   added. A location that cannot be read as a file is a group of its own.
    /// </summary>
    /// <param name="locations">The locations, each with what it is on disk.</param>
    /// <returns>The primary location ID of each location, by location ID.</returns>
    public static Dictionary<int, int> FindPrimaries(IReadOnlyList<(VideoLocal_Place Location, OnDisk Disk)> locations)
    {
        var primaries = new Dictionary<int, int>();
        foreach (var group in locations.GroupBy(tuple => tuple.Disk.Identity is { } identity ? (object)identity : tuple.Location))
        {
            var members = group.OrderBy(tuple => tuple.Location.ID).ToList();
            var primary = (members.FirstOrDefault(tuple => !tuple.Disk.IsSymbolicLink).Location ?? members[0].Location).ID;
            foreach (var (location, _) in members)
                primaries[location.ID] = primary;
        }

        return primaries;
    }
}
