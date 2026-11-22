# File System

`IFileSystem` is how the server itself reads and changes files, offered to
plugins so they do it the same way. It is not an extension point. Inject it:

```csharp
public class MyLinker(IFileSystem fileSystem) { }
```

Every call takes absolute paths and works around Windows' 260-character path
limit, which plain `System.IO` calls hit under deep library folders.

---

## Making files

| Member | What it does |
|---|---|
| `CopyFile(source, destination, overwrite)` | A new file with the same contents. |
| `MoveFile(source, destination, overwrite)` | Moves a file, across volumes too. On one volume, replacing a file is a single step, so readers never see neither. |
| `MoveDirectory(source, destination, overwrite)` | Renames a directory on one volume, moves it entry by entry across volumes, and with `overwrite` merges it into one already there. Links inside move as links. |
| `CreateHardLink(source, destination)` | A second name for the same file. Both paths must be on the same volume, and an `IOException` says when they are not. |
| `CreateSymbolicLink(source, destination)` | A link pointing to the source, a file, a directory or another link, which must exist; a relative source is read from the destination's folder and stored as given. Windows only allows it to an administrator or with developer mode on. |
| `CanCreateSymbolicLinks(directory)` | Whether links can be made in that directory, by trying. It depends on the volume, so check each directory links go to. |
| `DeleteFile`, `CreateDirectory`, `DeleteDirectory` | As in `System.IO`. |

Both moves change only the case of a name (`afile.mkv` to `AFile.mkv`) even on
a case-insensitive file system, where both spellings name the same entry, by
going through a temporary name.

None of them touches the database, and the server's file watcher does not see
them, so a file moved here is not taken for a new one. A video file made in a
managed folder this way is found by the next scan, or at once when handed to
`IVideoService.NotifyVideoFileChangeDetected`: a symbolic link or a hard link
of a known file joins that file's video without hashing, a copy is matched by
its name and size or hashed. To move a video's file and keep its record, use
`IVideoRelocationService` instead.

---

## Reading an entry

`GetStat(path)` reads what the file system says about an entry in one call where the platform allows (`statx` on Linux, one handle
on Windows):

| Type | What it has |
|---|---|
| `FileSystemStat` (all) | `Path`, `Identity`, `LinkCount`, `LastWriteTime`, `LastAccessTime`, `ChangeTime`, `CreationTime` |
| `FileStat` | `Size`, `AllocatedSize` |
| `DirectoryStat` | nothing more |
| `SymbolicLinkStat` | `Target`, as stored in the link, and `IsDirectoryLink`: known on Windows, where links are made as file or directory links, and `null` on Unix, where they have no kind |
| `SpecialFileStat` | `Kind` (pipe, socket, character or block device) and, for a device, `DeviceID`; Unix only |

`GetFileStat` and `GetDirectoryStat` read the same and give the
result only when it is a file, or a directory.

- Times are UTC. `CreationTime` is `null` where it is not kept: Unix other than
  Linux, or a Linux file system without birth times. `ChangeTime` is when the
  contents or metadata last changed (`ctime`), not the creation time.
  `LastAccessTime` is a hint, since many systems update it lazily.
- A file's `LinkCount` above one means it has hard links. A directory's means
  little, and differs between file systems.
- A symbolic link answers for what it points to, and a link to nothing reads as
  `null`. Pass `followSymbolicLinks: false` to read the link itself, as a
  `SymbolicLinkStat`.
- `null` means nothing is at the path, or the platform cannot tell. Anything
  else that
  goes wrong throws: `UnauthorizedAccessException` when it may not be read,
  `IOException` otherwise (a mounted share whose server is unreachable, for
  one). Permissions and ownership are not read.
- **An unmounted volume reads as nothing there.** Its mount point is an empty
  local folder, or gone, so a file under it is "not found", the same as a
  deleted one. Before acting on a `null`, such as removing what you keep about
  a file, check that the volume is still there: that the folder above it still
  exists, is not empty, and has the `Identity.VolumeID` you saw before. The
  core does this before it removes the record of a missing video file.
- A hard-mounted NFS share whose server is gone makes the call wait, without an
  error, until the server is back.

`ResolveSymbolicLink(path)` follows a link through any chain of links to the
final target, or with `returnFinalTarget: false` only to its own target, and
gives `null` for anything that is not a link. A chain that leads back to a link
already followed, or goes through more than 40 links, throws a
`SymbolicLinkLoopException` (an `IOException`) naming where the walk started
and where the loop closed.

`GetFileInfo`, `GetDirectoryInfo` and `GetFileSystemInfo` give .NET's own
`FileInfo` and `DirectoryInfo` instead, for code that wants those.

---

## Reading a volume

`GetVolumeMounts()` lists the mounted volumes as `VolumeMount`s, from the
system's list of mounts alone (`/proc/self/mountinfo` on Linux, .NET's drive
list elsewhere), so a share whose server is gone cannot hold it up. By default
it lists what the folder browser offers; `includeSystem: true` adds the
system's own mounts (`IsSystem`), and `includeStats: true` reads each volume
too, giving a `VolumeStat` for each one that can be read.

`GetVolumeStat(path)` reads the volume an entry is on. A `VolumeStat` is a
`VolumeMount` read further:

| Member | What it is |
|---|---|
| `MountPoint` | The volume's root: `/mnt/media`, `C:\`, `\\server\share\` |
| `Source` | The device or share mounted there (`/dev/sda1`, `//server/media`), on Linux and for Windows shares |
| `FormatName` | `ext4`, `btrfs`, `cifs`, `nfs4`, `NTFS` and so on |
| `Kind` | `Fixed`, `Removable`, `Network`, `Optical`, `Memory`, or `Generic` |
| `IsReadOnly` | Whether it is mounted read-only; on a listed mount, Linux only |
| `IsSystem` | Whether it is the system's own rather than storage |
| `Path` | The path it was read for (`VolumeStat` only, as are those below) |
| `VolumeID` | The same value as the entry's `Identity.VolumeID` |
| `Label` | The volume's label, Windows only |
| `TotalSize`, `FreeSpace`, `AvailableFreeSpace` | In bytes; the last is what the server may use |

Reading a volume touches it: on Linux `statvfs`, with `/sys` telling a
removable disk; elsewhere on Unix `statvfs` too; on Windows the volume the
path is under. A hard-mounted share whose server is gone makes it wait.

---

## Telling files apart

`Identity`, a `StatIdentity`, is the volume an entry is on and its ID there
(the device and inode number on Unix, the volume serial number and file ID on
Windows).

- Two paths are the same file, hard links of each other included, when the two
  identities are equal.
- Both values are opaque lowercase hexadecimal. Compare them, nothing else.
- Compare identities read at the same time. A Unix device number can change
  across a reboot or a remount, and a deleted file's ID can be given to a new
  one.
- Some network mounts make up IDs, so do not treat a match there as certain.

`IVideoFile.Stat` gives the whole info for a video's location, and `null`
when it cannot be read. Compare identities, not whole `FileSystemStat` records,
whose times differ between reads.

---

## Listing

`GetFilePaths` and `GetDirectoryPaths` list paths, and `GetFileEntries`,
`GetDirectoryEntries` and `GetFileSystemEntries` list `FileSystemEntry`s. Each
takes a `FileSystemListingOptions`; without one it lists the directory itself,
links included. Subdirectories are walked in parallel, so the results come in
no particular order, and one that cannot be read is skipped.

| Option | What it does |
|---|---|
| `Recursive` | Look in subdirectories too. |
| `Extensions` | Only list files with these extensions; directories are not filtered by them. |
| `Filter` | A `FileSystemEntryFilter` deciding which entries to keep; a directory left out is not walked. |
| `FollowDirectoryLinks` | Walk into links to directories. Off by default, as such a link can lead out of the directory or back up into it; when on, a directory reached through a link is walked once. |
| `IncludeDirectoryLinks` | List links to directories, walked into or not. On by default. |
| `IncludeFileLinks` | List links to files, and links whose target is gone. On by default. |

A `FileSystemEntry` knows its `Path`, `Name`, `IsDirectory` and `IsSymbolicLink`
from the listing itself, at no cost, and reads its `Stat` (with `FileStat` and
`DirectoryStat` beside it) only when first asked, then keeps it. The filter,
ignore rules (`IManagedFolderIgnoreRule`), the detected-file event and hashing
requests all hand out entries, so code that only needs the path costs a scan
nothing, and code that needs the size or times pays one read for that entry.
Whether an entry is a special file, such as a pipe, is not known from the
listing: `Stat is SpecialFileStat` tells.
