# What is Shoko Server
Shoko makes managing your anime collection effortless. It ensures that your collection is well-organized, accessible,
and easy to navigate. Regardless of whether you have a small or large anime collection, Shoko can handle it. Thanks to
its scalability, it can grow alongside your collection with ease.

With Shoko, you'll have access to your entire collection both locally and over the internet, with no additional work
required outside the initial configuration of Shoko Server and one of the supported media player plugins. Say
goodbye to manually inputting information or renaming your files to a specific format just to obtain basic series
data - with Shoko, this is a thing of the past.

Shoko takes the hassle out of managing your anime collection. With its user-friendly interface, you can sit
back and let it do the work for you. No more manual inputting or renaming - just effortless organization and access to
your favorite anime.

[Learn More About Shoko](https://shokoanime.com)
[User Docs](https://docs.shokoanime.com/getting-started/installing-shoko-server)

# Supported Media Players
Shoko currently supports the following media players.

- Plex via **Shoko Relay** (server plugin) [Download](https://github.com/natyusha/ShokoRelay/releases/) | [Github Repo](https://github.com/natyusha/ShokoRelay)
- Plex via **Shoko Relay** (Plex bundle) [Download](https://github.com/natyusha/ShokoRelay.bundle/releases/) | [Github Repo](https://github.com/natyusha/ShokoRelay.bundle)
- Plex via **ShokoMetadata** [Download](https://github.com/Cazzar/ShokoMetadata.bundle/releases/) | [Github Repo](https://github.com/Cazzar/ShokoMetadata.bundle)
- Jellyfin via **Shokofin** [Download](https://github.com/ShokoAnime/Shokofin/releases/) | [Github Repo](https://github.com/ShokoAnime/Shokofin)
- Kodi via **Nakamori** [Download](https://shokunin.monogatari.pl/projects/nakamori/nakamori-installation/) | [Github Repo](https://github.com/bigretromike/nakamori/)
- Kodi via **Shokodi** [Download](https://github.com/ShokoAnime/Shokodi/releases/) | [Github Repo](https://github.com/ShokoAnime/Shokodi)

Don't see your media player above? If you're a developer who wants to integrate Shoko with a new media player, join our
Discord, and we'll be more than happy to provide guidance and assistance.

**At this time, the Shoko team itself has no plans to integrate Shoko with any other media players.**

# Bundled Plugins
Official builds ship with these first-party plugins. The copies shipped with the server load as system plugins, are
enabled by default and cannot be uninstalled; an update installed over one into the user data `plugins` folder can be:

- **TMDB** ([`Shoko.Plugin.Tmdb`](https://github.com/ShokoAnime/Shoko.Plugin.Tmdb), developed in its own repository and
  bundled from its latest release) supplies The Movie Database metadata for the `tmdb` source: shows, seasons, episodes,
  movies, collections, people, studios and networks, their titles, overviews and images, and alternate orderings. It is
  configured from its plugin settings (`tmdb.json` in the plugin's configuration folder), not from the server settings.
- **WebAOM** ([`Shoko.Plugin.WebAOM`](https://github.com/ShokoAnime/Shoko.Plugin.WebAOM), developed in its own repository
  and bundled from its latest release) holds the WebAOM renamer, the default relocation provider. It covers basic file
  names and is feature-frozen; for a folder structure of your own or several managed folders, use the
  [Lua renamer](https://github.com/Mik1ll/LuaRenamer) or a custom relocation provider.
- **Offline Importer** ([`Shoko.Plugin.OfflineImporter`](https://github.com/ShokoAnime/Shoko.Plugin.OfflineImporter),
  developed in its own repository and bundled from its latest release) imports releases from file names.
- **Legacy Plex Sync Support** (`Shoko.Plugin.LegacyPlex`) holds the legacy Plex support: the `/plex` webhook and
  routes, the Plex account linking and the watched state sync. Disabling the plugin turns all of it off. For Plex,
  [Shoko Relay](https://github.com/natyusha/ShokoRelay) takes over from it.

Other plugins are installed into the `plugins` folder under `SHOKO_HOME`.

Legacy Plex Sync Support is built with the server. The others are released from their own repositories, and a build
from source leaves them out unless asked: pass `-p:ShokoBundleReleasedPlugins=true` to bundle their latest stable
releases, each checked against the SHA-256 its published manifest gives. Without it, install them from the Shoko Core
Repository in the Web UI. A build without network, such as a packaging sandbox, can fill `.cache/plugins/` itself and
add `-p:ShokoReleasedPluginsOffline=true`; `ReleasedPlugins.targets` describes the layout.

# Docker

| variable | default | what |
|---|---|---|
| `SHOKO_HOME` | `/home/shoko/.shoko/Shoko.CLI` | where settings, logs and the database live — the directory to mount a volume at. A custom path must already exist; only the default is created for you, and it is required when running as root (`PUID=0`) |
| `ENABLE_RESTART` | `true` | allow the web interface to restart the server |
| `ENABLE_SHUTDOWN` | `false` | allow the web interface to shut the server down. Off by default because a stopped container will not restart itself from the web interface that just stopped it |
| `INSTALL_PACKAGES` | unset | extra apt packages to install before startup, space or comma separated |
| `EXTRA_GROUPS` | unset | supplementary groups for the server's user, by name or numeric ID, space or comma separated |

`PUID`, `PGID`, `UMASK` and `NO_CHOWN` are also honoured, and the effective
values are printed in the startup banner.

What lives under `SHOKO_HOME`:

| folder | what |
|---|---|
| `SQLite` | the server's own database, unless the database settings point elsewhere |
| `data` | the plugins' databases, one folder per plugin ID |
| `configuration` | configuration files, one folder per plugin ID |
| `cache` | the plugins' caches; safe to empty while the server is stopped |
| `plugins` | installed plugins, nothing else; the bundled plugins live next to the server, not here |
| `DatabaseBackup` | copies of the databases taken before an upgrade or a plugin migration |
| `images`, `logs`, `themes`, `transcodes` | images, logs, Web UI themes and streaming renditions |

Back up everything but `cache` and `transcodes`.

`INSTALL_PACKAGES` installs extra apt packages before the server starts.
Space or comma separated, unset by default. It exists for userspace the image
cannot ship for everyone but that has to be present *before* startup — GPU
drivers for hardware transcoding above all, since plugins probe the hardware
while starting and cache what they find.

```yaml
environment:
  - INSTALL_PACKAGES=libva2 libva-drm2 intel-media-va-driver-non-free ffmpeg
```

Packages live in the container's writable layer, so they survive a restart but
not a recreate, and are reinstalled on the next start when that happens. A
package that fails to install is a warning, not a failure to boot.

`EXTRA_GROUPS` grants the server access to a passed-through device — `/dev/dri`
for GPU transcoding above all, which is owned by the host's `render` group:

```yaml
devices:
  - /dev/dri:/dev/dri
environment:
  - EXTRA_GROUPS=993        # stat -c '%g' /dev/dri/renderD128, on the host
```

Prefer the numeric ID: group *names* differ between distributions and need not
exist in the container at all, while the kernel only compares numbers. An ID
with no group behind it gets one created.

Note that Docker's own `--group-add` cannot do this. It adds groups to the
container's root process, and the groups that survive dropping privileges are
the ones recorded against the user — which is what `EXTRA_GROUPS` writes.

# Building Shoko

Install the latest .net sdk

## Windows:
Build TrayService or CLI from VS Code or command line via:

`dotnet build Shoko.TrayService/Shoko.TrayService.csproj`

## Linux:
Install mediainfo and rhash. For apt, that would be:

`sudo apt install mediainfo librhash-dev`


Build from CLI:

`dotnet build -c=Release -r linux-x64 -f net10.0 Shoko.CLI/Shoko.CLI.csproj`

Building `Shoko.CLI` or `Shoko.TrayService` also builds the bundled plugins (listed in `BundledPlugins.targets`) and
copies each one to `plugins/<project name>/` in the output.

If that doesn't work, this document may be out of date. Check the dockerfile for guaranteedly updated build steps.

# Contributing

We are always accepting help, and there are a million little things that always need done. Hop on our [discord](https://discord.gg/shokoanime) and talk to us. Communication is important in any team. No offense, but it's difficult to help anyone that shows up out of nowhere, opens 3 issues, then creates a PR without even talking to us. We have a wealth of experience. Let us help you...preferably before the ADHD takes over, you hyperfixate, and you come up with a fantastic solution to problem that isn't at all what you expected. Support is also best found in the discord, in case you read this far.

![Alt](https://repobeats.axiom.co/api/embed/c233a2de69d1f2f56e4cbe96b4b4cd33dc223d19.svg "Repobeats analytics image")
