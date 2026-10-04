# Shoko WebAOM Renamer

The bundled first-party plugin holding the WebAOM renamer, the server's
default relocation provider. It ships with the server in
`plugins/Shoko.Plugin.WebAOM/`, is enabled by default and cannot be
uninstalled.

## What it does

`WebAOMRenamer` is an ordinary relocation provider
(`IRelocationProvider<WebAOMSettings>`), named "WebAOM Renamer". It names
files with a script in the WebAOM scripting language (see
<https://wiki.anidb.net/WebAOM#Scripting>), and picks their folder by the
settings below.

- **Renaming.** The script is read line by line. A `DO` line always runs its
  action; any other line runs it only when its tests pass. `ADD` appends text
  with the tags expanded (`%ann`, `%eng`, `%enr`, `%grp`, `%res`, `%CRC` and
  the rest in `Constants.FileRenameTag`), `REPLACE` finds and replaces in the
  name so far, and `FAIL` stops the rename. The file keeps its extension, and
  invalid path characters are replaced.
- **Moving.** With `GroupAwareSorting` off, a file goes to the folder holding
  the series' latest episode when that folder has room, or else to the first drop
  destination with room, in a folder named after the AniDB anime. With it on,
  a file goes to `<group>/<series>` (or `<series>` when the group holds one
  series) in the first drop destination; restricted series prefer a
  destination whose path holds `Hentai`, and the rest avoid it.

The renamer needs the file's episodes: an unrecognized file is not renamed.

## Presets

Each relocation preset of the renamer holds its own `WebAOMSettings`,
edited through the relocation preset API.

| Setting | Default | |
|---|---|---|
| `MaxEpisodeLength` | `33` | Episode names longer than this, `1` to `250`, are cut short with `…`. |
| `GroupAwareSorting` | `false` | Places files by the Shoko group structure, as above. |
| `Script` | the sample | The WebAOM script. |

A new preset starts with a sample script that names files like
`[Group]_Title_-_01_(1920x1080_Blu-ray_H264)_[CRC32].mkv`. When the server
starts with no presets at all, the core creates one named "Default" with it.
With the plugin turned off, that waits for a start with it on.

## Thumbnail and icon

`Assets/thumbnail.svg` and `Assets/icon.svg` are embedded resources, read by
the server through the plugin.
