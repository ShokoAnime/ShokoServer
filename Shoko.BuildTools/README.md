# Shoko.BuildTools

Build tool for Shoko plugins. Wraps MSBuild with assembly metadata injection,
auto-discovery of service interfaces, and manifest management.

## What it does

1. **Reads** the plugin's identity and tags from its project or the command line, or
   from its `manifest.json` (if supplied) for what neither states, and its
   dependencies from the project or the command line alone. See
   [Plugin metadata](#plugin-metadata) and [Dependencies](#dependencies).
2. **Scans source files** with Roslyn syntax trees to auto-detect Shoko service
   interface implementations (`IReleaseInfoProvider` → `release-provider`,
   `IHostedService` → `hosted-service`, etc.) and prepends them to the tags.
3. **Generates** a C# assembly metadata file with:
   - Runtime identifier, release date, source revision, release tag
   - Package identity (`PackageID`, `PackageName`, `PackageOverview`, and
     `PackageNamespace` when the plugin claims one)
   - Dependencies serialized as `<guid>@<range>[:optional|:required][,<guid>@<range>[:optional|:required]]*` (`PackageDependencies`)
   - Tags as a single `PackageTags` attribute
4. **Loads** the `.csproj` in-memory via the MSBuild API, sets
   `EnableDynamicLoading=true`, injects the generated assembly info, restores
   and builds.
5. **Packs** the build output into a `.zip` archive.
6. **Computes** a SHA256 checksum, the packed size of the zip and the size of its files unpacked.
7. **Updates** `manifest.json` — creates or updates a release entry with the
   archive URL, checksum, packed and unpacked sizes, runtime, ABI version, dependencies, and whether the
   release is a meta plugin (`is_meta`, read from the built assembly).
8. **Prunes** old releases if `--prune` is set (per-channel or globally), naming
   every dropped version and its channel on stdout.

## Installation

```bash
dotnet tool install --global Shoko.BuildTools
```

Or install a specific version from a local package:

```bash
dotnet tool install --global --add-source ./nupkg Shoko.BuildTools
```

## Usage

```bash
shoko-build [options] [-- <msbuild-args>]
```

### Options

| Argument | Short | Description |
|----------|-------|-------------|
| `--manifest <path>` | `-m` | Path to the plugin's `manifest.json`. If omitted, the tool looks for `manifest.json` in the current directory, then next to the project/solution file. When found, the tool updates the manifest after build with release metadata, download URL, and checksum. |
| `--prune <count>` | `-p` | Maximum number of releases to keep in the manifest. Only used with `--manifest`. |
| `--prune-method <method>` | | Pruning method: `channel` (default, per-channel) or `global` (all releases together). |
| `--url <url>` | `-u` | **Required.** Download URL recorded for the archive in the manifest. Supports `{runtime}`, `{version}`, `{name}`, `{abstraction}` and `{tag}` templates. Recorded as given after substitution. |
| `--output <path>` | `-o` | **Required.** Output path for the packed `.zip` archive. Supports `{runtime}`, `{version}`, `{name}`, `{abstraction}` and `{tag}` templates. Intermediate directories are created. |
| `--tag <tag>` | `-t` | Release tag recorded in the assembly and the manifest entry, and used for `{tag}`. Defaults to the `v<major>.<minor>.<patch>` tag on `HEAD`, if there is one; `{tag}` then falls back to `v<version>`. |
| `--channel <name>` | | Release channel for the manifest entry: `Stable`, `Dev` or `Debug`, case-insensitive. An unrecognised name is an error. See below for more info. |
| `--release-notes <text>` | | Release notes recorded on the manifest entry. See below. |
| `--release-notes-path <path>` | | Read the release notes from a file instead. Mutually exclusive with `--release-notes`. |
| `--id <guid>` | | Plugin ID. See [Plugin metadata](#plugin-metadata). |
| `--name <name>` | | Plugin name, replacing the manifest's. |
| `--overview <text>` | | Plugin overview, replacing the manifest's. |
| `--namespace <name>` | | Plugin namespace, replacing the manifest's. See [Plugin namespace](#plugin-namespace). |
| `--tags <list>` | | Comma-separated tags, replacing the manifest's. Auto-detected tags are still added. |
| `--dependencies <list>` | | Plugin dependencies, replacing the project's. See [Dependencies](#dependencies). |

Both templates are evaluated per runtime identifier, so one invocation
covers a whole matrix:

```bash
--output 'dist/{name}-{tag}-{runtime}.zip' \
--url    'https://example.org/owner/repo/releases/download/{tag}/{name}-{tag}-{runtime}.zip'
```

All other arguments are forwarded to MSBuild. Common forwarded args:

| Argument | Description |
|----------|-------------|
| `-c Release` | Build in Release configuration. |
| `-p:Version=1.0.5` | Override the version. |
| `--no-restore` | Skip the restore. Same meaning as `dotnet build --no-restore`. |
| `-p:ReleaseChannel=Dev` | The channel this build belongs to. `Shoko.BuildTools.Targets` stamps it into the assembly as `ReleaseChannel`; this tool records it in the manifest entry. `--channel` overrides it. |

### Release notes

Three ways in, highest precedence first:

| Source | |
|--------|--|
| `--release-notes <text>` | Inline. |
| `--release-notes-path <path>` | From a file, so notes with newlines survive a shell. |
| `RELEASE_NOTES` | Environment variable, for CI that already has the notes in one. |

Passing both flags is an error rather than a precedence question, and a
path that cannot be read stops the build — recording no notes quietly is
the outcome this exists to prevent.

Leading and trailing whitespace is trimmed, and notes that are empty or
whitespace-only are recorded as `null` rather than `""`.

Notes are only written when supplied. A second runtime's archive joining
an existing release therefore does not blank the notes the first one
set, and re-running without the flag leaves what is already recorded.

### Release channel

Three ways to arrive at one, in order:

1. `--channel <name>`.
2. `-p:ReleaseChannel=<name>` — the same property `Shoko.BuildTools.Targets`
   stamps into the assembly.
3. Neither — inferred from the assembly version's revision number being above 0 for `Dev`, otherwise `Stable`.

Stated beats inferred; the two are never merged. An unrecognised name is
an error in both the tool and the targets package.

### Plugin metadata

The plugin's identity and tags come from the manifest unless stated, and its
dependencies only ever from the project or the command line, highest
precedence first:

| Flag | In the project | Manifest field |
|------|----------------|----------------|
| `--id` | `PluginID` | `id` |
| `--name` | `PluginName` | `name` |
| `--overview` | `PluginOverview` | `overview` |
| `--namespace` | `PluginNamespace` | `namespace` |
| `--tags` | `PluginTags` | `tags` |
| `--dependencies` | `PluginDependencies`, then `PluginDependency` items | |

A stated value replaces the manifest's, and lists are never merged. The
project is the truth: the name, overview, namespace and tags it states are also written
into the manifest the build updates, the discovered tags included, so a
release manifest always matches what the plugin says about itself. Change
them in the project, not in the manifest. The dependencies stated are what
the release entry records.
`Shoko.BuildTools.Targets` reads the same properties and items, so a plain
`dotnet build` takes them too. The properties go in the plugin's `.csproj`
like any other:

```xml
<PropertyGroup>
  <PluginName>Example Plugin</PluginName>
  <PluginTags>example,metadata</PluginTags>
</PropertyGroup>
```

When there is a manifest, `--id` has to match its `id`: the manifest is
updated with this build's release, so it must describe the same plugin. Unlike
the rest of the identity, the `id` is never written over: a mismatch almost
always means the build points at another plugin's manifest.
The targets package never writes the manifest and has no such check.

### Plugin namespace

A plugin may reserve a namespace: a short, unique name for itself, which
other plugins and conventions may key off. For example, a scripting plugin
might let other plugins add script modules named after their namespace
(`<namespace>:<name>`). It is
optional and stamped as `PackageNamespace`.

```xml
<PropertyGroup>
  <PluginNamespace>example</PluginNamespace>
</PropertyGroup>
```

- It is kebab-case, `^[a-z][a-z0-9]*(-[a-z0-9]+)*$`, and 2 to 32 characters
  long.
- Whichever source gives it, `--namespace`, the property or the manifest, the
  build sees it as the `PluginNamespace` property, for targets that read it.
- `shoko`, `core` and `plugin` are reserved.
- A namespace breaking either rule stops the build, naming where it came
  from.
- The server checks the same rules, and fails a plugin breaking them as it
  starts. Two loaded plugins claiming the same namespace both fail the
  start, each naming the other, until one is disabled, replaced or
  uninstalled.

### Dependencies

A plugin's dependencies are the plugins the server installs and loads
before it. They are build inputs: stamped into the assembly as
`PackageDependencies` and recorded in the release entry each build adds,
which is what the server resolves when it installs a release. So they belong
in the project, one `PluginDependency` item per plugin:

```xml
<ItemGroup>
  <!-- Example Library, which the plugin cannot run without. -->
  <PluginDependency Include="0f8a1c2e-1111-2222-3333-444455556666" Version="^1.2.0" />
  <!-- Example Extras, used when it is installed. -->
  <PluginDependency Include="7d3b9f40-aaaa-bbbb-cccc-ddddeeeeffff" Version=">=2.0" Optional="true" />
</ItemGroup>
```

- `Include` is the plugin's ID, a GUID.
- `Version` is required: `>=1.0.0`, `^1.0.0`, `~1.0.0` or an exact `1.0.0`.
- `Optional` is `true` or `false`, and `false` when left out. A required
  dependency that is missing, disabled or of another version keeps the
  plugin from loading; an optional one is loaded first when it is there, and
  ignored when it is not.
- An item that does not parse, a range the server cannot evaluate, or a
  plugin listed twice stops the build, naming the item.

They are an ordinary item group, so a `Condition` on an item, or items added
from a `Directory.Build.props`, work as anywhere else, and a version may come
from a property, such as `Version=">=$(ExampleLibraryVersion)"`.

A manifest has no top-level `dependencies`: a build that finds them there
warns and ignores them.

### Generated plugin class

Most plugins' `IPlugin` class only states their ID, name and description,
which the project already holds. Set `PluginGenerateClass` and the class is
generated instead, so the project is the one place the plugin's identity is
written:

```xml
<PropertyGroup Label="Plugin">
  <PluginGenerateClass>true</PluginGenerateClass>
  <PluginID>1a2b3c4d-0000-0000-0000-000000000000</PluginID>
  <PluginName>Example Plugin</PluginName>
  <PluginOverview>Does a useful thing.</PluginOverview>
  <PluginTags>example</PluginTags>
  <PluginIcon>Assets/icon.svg</PluginIcon>
  <PluginThumbnail>Assets/thumbnail.svg</PluginThumbnail>
</PropertyGroup>
```

`Shoko.BuildTools.Targets` writes a `partial` class, `Plugin` in the project's
`RootNamespace` (`PluginClassName` names it otherwise), whose `ID`, `Name` and
`Description` come from what the build stamps into the assembly. The rest of
the plugin refers to it as usual, as in `PluginPaths<Plugin>`. To add a main
configuration or behaviour, write the rest of the class:

```csharp
public partial class Plugin : IPlugin<ExampleConfiguration>
{
    public void Setup(IServiceProvider serviceProvider) { … }
}
```

A **meta plugin** is a generated class with no other code: a plugin that only
brings in the plugins it depends on, such as a set meant to be installed
together. Installing it installs them as dependencies, they are enabled while
it is, and uninstalling it removes the ones nothing else needs. Optional
dependencies are listed by the server but not installed unless asked for.

The targets detect a meta plugin as they build it and stamp the result as
`PackageIsMeta` (`true` or `false`): the project sets
`PluginGenerateClass` and compiles no source of its own, only files generated
into its intermediate (`obj`) folder. A project with
`EnableDefaultCompileItems` set to `false` and no sources counts too. No
property sets it by hand. `shoko-build` reads it back from the built assembly
into the release entry's `is_meta`, and sets the manifest's own `is_meta`:
`true` when every release is a meta plugin, `false` when any is not, and left
out when no release says either way. The server shows them as `IsMeta` on the
plugin, the package and the package release, so a client can group the
dependencies under their meta plugin before anything is installed. A plugin
built without the targets has no mark, and each reads as `null`.

### Plugin images

`PluginIcon` and `PluginThumbnail` name the image files to show for the
plugin: a square icon, readable at 24 pixels, and a wide thumbnail. The build
embeds them under names rooted in the assembly and stamps those names into it,
which the server reads, with or without a generated class. They replace
`EmbeddedResource` items paired with `EmbeddedIconResourceName` and
`EmbeddedThumbnailResourceName` in the plugin's class, which the server still
reads when nothing is stamped. SVG is preferred; PNG, JPEG, WebP, GIF, BMP and
TIFF work too.

### Dependency list

`--dependencies` and the `PluginDependencies` property take the same
dependencies as one string, the format of the assembly's
`PackageDependencies` metadata. Either replaces the project's
`PluginDependency` items, the property with a warning when the project has
some, so `-p:PluginDependencies=...` overrides them for one build.

```
<guid>@<range>[:optional|:required][,<guid>@<range>[:optional|:required]]*
```

- `<range>` is `>=1.0.0`, `^1.0.0`, `~1.0.0` or an exact `1.0.0`.
- A dependency is required unless marked `:optional`.
- Whitespace around `,`, `@` and `:` is ignored, and so are empty
  entries, so a list can span lines.
- The older `<guid>:<range>[:true]` form is still read.
- An entry that does not parse, a range the server cannot evaluate, or a
  plugin listed twice stops the build. The manifest's `dependencies` are
  held to the same rules.

The assembly's `PackageDependencies` metadata uses the same format, so a
list read off one build can be handed to the next.

Quote the list: `>=` is a shell redirect, and `^` and `~` are globs in
some shells.

```bash
shoko-build -c Release --output ./dist/plugin.zip --url https://example.org/plugin.zip \
  --dependencies '0f8a1c2e-1111-2222-3333-444455556666@^1.2.0,
                  7d3b9f40-aaaa-bbbb-cccc-ddddeeeeffff@>=2.0:optional'
```

MSBuild splits `-p:` values on commas, so with `dotnet build` and the
targets package the quotes have to reach MSBuild, or the commas have to
be escaped as `%2C`:

```bash
dotnet build '-p:PluginDependencies="0f8a1c2e-1111-2222-3333-444455556666@^1.2.0,7d3b9f40-aaaa-bbbb-cccc-ddddeeeeffff@>=2.0:optional"'
```

### Exit code

`0` only when **every** requested runtime identifier produced a recorded
archive. Otherwise the run exits `1` and names what is missing. Entries
that did succeed are still written to the manifest.

### Runtime identifier agreement

The `runtime` recorded in the manifest is checked against the one the
built assembly is stamped with — a `Directory.Build.targets`, a shared
props file or `UseCurrentRuntimeIdentifier` can set its own after this
tool has chosen one. When the two disagree the archive is **not**
recorded and the run fails.

Portable builds pass **no** `RuntimeIdentifier` to MSBuild at all. `any`
is stamped when, and only when, there is genuinely no RID.

Forwarded properties, the runtime identifier among them, are set as
MSBuild **global** properties, so a project cannot redefine the runtime
identifier it was asked to build for.

### Restore

The tool restores before each build. The assets file is
per-runtime-identifier, so a matrix run restores once per RID; pass
`--no-restore` if the caller has already done it.

### What goes into the archive

Everything in the build's output directory, minus the archive itself
when `--output` names a path inside it, a leftover `publish/`
subdirectory, and any subdirectory holding its own copy of the plugin
assembly — a stale per-RID output directory. Nothing the build produced
is removed.

### MSBuild output

MSBuild diagnostics are forwarded at `Minimal` verbosity. Set
`SHOKO_BUILD_VERBOSITY` (`Quiet`, `Minimal`, `Normal`, `Detailed`,
`Diagnostic`) to change that.

### Manifest hooks

A plugin's build can add its own data to the `extensions` of a release
and of its archives. Without a manifest, or with nothing attached to a
hook, that hook does not run.

The hooks run in this order, none of them building again:

1. **`ShokoBeforeAssetWrite`** runs once per runtime, on that runtime's
   built project, as soon as its archive is packed. Its pending file,
   `ShokoPendingAssetPath`, holds the archive entry as the manifest writes
   it.
2. **`ShokoBeforeReleaseWrite`** runs once per release, after every
   runtime, on the project evaluated without a runtime. Its pending file,
   `ShokoPendingReleasePath`, holds the release entry with all of its
   archives, the asset hooks' `extensions` included.
3. **The manifest is saved.**
4. **`ShokoAfterReleaseWrite`** runs once per release, with the same
   properties as `ShokoBeforeReleaseWrite`, except that the pending file is
   empty.

Attach to a hook with `AfterTargets="<hook>"` or through
`<hook>DependsOn`, and edit the pending file in place.

**What is read back.** Only `extensions` are taken from a pending file:
the archive's from an asset file, and from a release file the release's
and each archive's, matched by `runtime`. Each is merged key by key into
what the manifest has:
- objects merge;
- a `null` removes the key;
- anything else replaces it.

Every other field stays as the tool wrote it.

**When a hook fails.** This covers a failed hook, a pending file that is
not valid JSON, and an `extensions` that is not an object:
- an asset hook: its runtime is not recorded, as if its build had failed;
- the release hook: the manifest is not written;
- the after hook: the tool exits non-zero.

Properties set for the hooks:

| Property | Hooks | Value |
|----------|-------|-------|
| `ShokoManifestPath` | all | The manifest's full path. |
| `ShokoPendingAssetPath` | asset | The pending archive file. |
| `ShokoPendingReleasePath` | release | The pending release file. Empty in `ShokoAfterReleaseWrite`. |
| `ShokoReleaseKey` | all | The release's `version` in the manifest, e.g. `0.1.0.5`. |
| `ShokoReleaseVersion` | all | The same as semver, e.g. `0.1.0-dev.5`. |
| `ShokoReleaseChannel` | all | `Stable`, `Dev` or `Debug`. |
| `ShokoReleaseRuntime` | asset | The runtime identifier, or `any`. |
| `ShokoReleaseRuntimes` | release | The runtimes this run recorded, separated by `;`. |
| `ShokoAssetName` | asset | The archive's file name. |
| `ShokoAssetPath` | asset | The archive's full path. |
| `ShokoAssetUrl` | asset | The archive's download URL. |
| `ShokoAssetSize` | asset | The archive's size in bytes (`packed_size`). |
| `ShokoAssetUnpackedSize` | asset | The sum of its files' sizes in bytes (`unpacked_size`). |
| `ShokoAssetChecksum` | asset | The archive's `checksum`, e.g. `sha256:…`. |

```xml
<Target Name="StampMyExtensions" AfterTargets="ShokoBeforeReleaseWrite">
  <!-- Read $(ShokoPendingReleasePath), set extensions.my-plugin, write it back. -->
</Target>
```

### Auto-detected tags

The source probe maps Shoko service interfaces to discovery tags:

| Interface | Tag |
|-----------|-----|
| `IReleaseInfoProvider` | `release-provider` |
| `IHashProvider` | `hasher` |
| `IRelocationProvider` | `relocator` |
| `IManagedFolderIgnoreRule` | `managed-folder-ignore-rule` |
| `IResourceResolver` | `resource-resolver` |
| `IMetadataResolver` | `metadata-resolver` |
| `IMetadataImageContributor` | `metadata-image-contributor` |
| `IAiringScheduleProvider` | `airing-scheduler` |
| `ISuspensionProvider` | `suspension-provider` |
| `IMetadataProvider` | `metadata-provider` |
| `IMetadataSeriesProvider` | `metadata-series-provider` |
| `IMetadataMovieProvider` | `metadata-movie-provider` |
| `IMetadataSeriesLinkingProvider` | `metadata-series-linker` |
| `IMetadataMovieLinkingProvider` | `metadata-movie-linker` |
| `IMetadataAutoLinkingProvider` | `metadata-auto-linker` |
| `IHostedService` | `hosted-service` |
| `IPluginServiceRegistration` | `service-registration` |
| `IPluginApplicationRegistration` | `application-registration` |

Only these interfaces add a tag; implementing any other adds none.

## Manifest format

See `manifest.schema.json` in this directory for the full JSON Schema.

```json
{
  "$schema": "https://raw.githubusercontent.com/ShokoAnime/ShokoServer/refs/heads/master/Shoko.BuildTools/manifest.schema.json",
  "id": "00000000-0000-0000-0000-000000000001",
  "name": "Example Plugin",
  "overview": "Example plugin.",
  "authors": "Example Author",
  "repository_url": "https://github.com/user/Shoko.Plugin.ExamplePlugin",
  "tags": ["example"],
  "releases": [
    {
      "version": "1.0.0",
      "tag": "v1.0.0",
      "released_at": "2026-01-16T14:12:10Z",
      "channel": "Stable",
      "archives": [
        {
          "runtime": "any",
          "abstraction": "6.0.0",
          "url": "https://github.com/user/Shoko.Plugin.ExamplePlugin/releases/download/v1.0.5/plugin.zip",
          "checksum": "sha256:<sha256>",
          "packed_size": 123456,
          "unpacked_size": 345678
        }
      ],
      "dependencies": [
        { "id": "<guid-of-dependency>", "version": ">=1.0.0" }
      ]
    }
  ]
}
```

The dependencies are per release: each entry records those its build stated
(see [Dependencies](#dependencies)), and the server resolves them when it
installs that release.

Besides the fields above, a manifest may give `homepage_url`, a wide
`thumbnail_url` and a square `icon_url` (readable at 24 pixels). The
deprecated `image_url` is read as the thumbnail when there is no
`thumbnail_url`. A package repository serves one such manifest, or a list
of them. A release can also carry
`source_revision`, `release_notes` and `is_meta`, which `shoko-build`
writes when the build stamped it, and sums up in the manifest's own `is_meta`
(see [Generated plugin class](#generated-plugin-class)).

A plugin can embed its thumbnail and icon in its assembly instead; see
[Plugin images](#plugin-images).

### Examples

Build and update the manifest:

```bash
shoko-build -c Release \
  --manifest manifest.json \
  --output ./dist/plugin.zip \
  --url https://example.org/owner/repo/releases/download/v1.0.5/plugin.zip
```

A whole runtime matrix from one invocation:

```bash
shoko-build -c Release \
  --manifest manifest.json \
  --output 'dist/{name}-{tag}-{runtime}.zip' \
  --url 'https://example.org/owner/repo/releases/download/{tag}/{name}-{tag}-{runtime}.zip'
```

Build with pruning (keep 5 most recent releases per channel):

```bash
shoko-build -c Release --manifest manifest.json \
  --output ./dist/plugin.zip --url https://example.org/plugin.zip --prune 5
```

Forward extra MSBuild properties:

```bash
shoko-build -c Release --manifest manifest.json \
  --output ./dist/plugin.zip --url https://example.org/plugin.zip \
  -- -p:Version=1.0.5 -p:TargetFramework=net10.0
```

### Integration with plugin `.csproj`

When using `shoko-build`, plugins need no `PluginAssemblyVersion` MSBuild
target, `EnableDynamicLoading` or `IncludeSourceRevisionInInformationalVersion`
in their `.csproj`. The tool handles all of that, and
[`Shoko.BuildTools.Targets`](#shokobuildtoolstargets) sets `EnableDynamicLoading`
for a plain `dotnet build` too. A minimal plugin `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Library</OutputType>
    <Version>1.0.5</Version>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Shoko.Abstractions" Version="..." ExcludeAssets="runtime" />
  </ItemGroup>
</Project>
```

A plugin that needs other plugins lists them as `PluginDependency` items; see
[Dependencies](#dependencies).

Keep `ExcludeAssets="runtime"` on the `Shoko.Abstractions` reference. The tool
packs everything in the build's output directory, and without it your plugin
ships its own copy of `Shoko.Abstractions.dll`, which makes the server skip the
plugin without an error. A plugin with queue jobs also references
`Shoko.QueueProcessor`, with `ExcludeAssets="runtime;native"`. Keep the two
on matching versions: the same stable version for a stable server, the latest
prereleases of both for daily.

`GenerateDocumentationFile` is what lets a setting fall back to its XML doc
summary for its description; without it those descriptions are empty. See the
[configuration README](../Shoko.Abstractions/Config/Services/README.md#descriptions).
It also warns about every undocumented public member (`CS1591`); add
`<NoWarn>$(NoWarn);CS1591</NoWarn>` if you'd rather not see those.

## Shoko.BuildTools.Targets

The MSBuild half of the same metadata, for a plain `dotnet build` (and for
local builds that never run `shoko-build`). Reference it as a development
dependency:

```xml
<PackageReference Include="Shoko.BuildTools.Targets" Version="..." PrivateAssets="All" ExcludeAssets="runtime" />
```

Its targets run before `CoreGenerateAssemblyInfo` and stamp the assembly with:

- `RuntimeIdentifier`: the build's runtime identifier, or `any` without one.
- The plugin's identity and tags (`PackageID`, `PackageName`,
  `PackageOverview`, `PackageNamespace`, `PackageTags`). They come from
  `manifest.json` in the project's directory or its parent. Without a manifest, the ID, name and
  description are read from the plugin class's source with Roslyn, following
  an ID derived from a type name through `UuidUtility.GetV5`. Either way, the
  `PluginID`, `PluginName`, `PluginOverview`, `PluginNamespace` and
  `PluginTags` properties win,
  as described in [Plugin metadata](#plugin-metadata).
- The plugin's dependencies (`PackageDependencies`), from the project's
  `PluginDependency` items or its `PluginDependencies` property, as described
  in [Dependencies](#dependencies).
- `PackageIsMeta`: whether the plugin is a meta plugin, detected just
  before compiling, as described in
  [Generated plugin class](#generated-plugin-class). Stamped under
  `shoko-build` too, which never writes it itself.
- `ReleaseChannel`, only when the `ReleaseChannel` property is set. An
  unrecognised name fails the build.
- `ReleaseDate`, `SourceRevision` and `ReleaseTag` from the repository, the
  tag being a `v<major>.<minor>.<patch>` tag on `HEAD`.

It also sets `EnableDynamicLoading` to `true` when the project leaves it unset,
as `shoko-build` does. That copies the plugin's own NuGet dependencies into its
output, where the server looks for them through the plugin's `.deps.json`; a
package the server ships stays out with `ExcludeAssets="runtime"` on its
reference. The default comes from a `.props` file NuGet imports ahead of the
project, since the SDK settles what a build copies before a package's
`.targets` load. A project that references the package but is not a plugin
sets `<EnableDynamicLoading>false</EnableDynamicLoading>`. The targets never
write the manifest.

A build that imports the targets from source instead of the package, as the
server does, leaves the `.props` out, so a plugin built that way sets
`EnableDynamicLoading` itself. It points `ShokoBuildToolsTasksAssembly` at the tasks it built,
names the targets that put them there in `ShokoBuildToolsPrepareTargets`, and
sets `ShokoBuildToolsUseTaskHost` to `true` so the tasks run out of process.

### Third-party notices

Every build also writes `third-party-notices.json.br` next to the assembly: the
licences of the NuGet packages the build ships, which the server lists for its
users together with its own. A plugin needs no code for it; the server finds the
file in the plugin's folder.

What is listed is what the build copies to its output (`RuntimeCopyLocalItems`
and the native, resource and runtime-target ones), so the shared framework and
packages referenced with `ExcludeAssets="runtime"` are left out. So are the
packages the server ships itself, `ThirdPartyNoticesHostPackages`
(`Shoko.Abstractions;Shoko.QueueProcessor` by default) and everything they depend
on, as `project.assets.json` records it.

For each package the targets read its nuspec (`<license type="expression">` or
`<license type="file">`, the copyright and the project URL) and the licence and
notice files at its root: `LICENSE*`, `LICENCE*` and `COPYING*` as licences,
`NOTICE*`, `THIRD-PARTY*` and `AUTHORS*` as notices (an Apache-2.0 `NOTICE` has
to be carried). What a package says is cached per exact ID and version in
`ThirdPartyNoticesCacheDirectory` (`obj/third-party-notices-cache/` by default),
which a clean or a rebuild leaves alone, since a package in the NuGet cache never
changes. The notices themselves are only written again when
`project.assets.json`, the project file or a listed text file changes.

A package whose licence cannot be read, with no SPDX expression and no licence
file but at most a URL, is warning `SHOKO0201`, and an error in CI
(`ContinuousIntegrationBuild`, `CI` or `GITHUB_ACTIONS` set to `true`, or
`ThirdPartyNoticesTreatMissingAsError`). Set its licence by hand, optionally for
one version:

```xml
<ItemGroup>
  <ThirdPartyNoticeOverride Include="Some.Package" License="MIT" TextFile="licenses/some-package.txt" />
  <ThirdPartyNoticeOverride Include="Other.Package" Version="1.2.3" License="Apache-2.0" />
</ItemGroup>
```

An asset no package carries, such as a font, is listed with `ThirdPartyNotice`:

```xml
<ItemGroup>
  <ThirdPartyNotice Include="Some Font" Version="2.0" License="OFL-1.1" TextFile="fonts/OFL.txt" Url="https://example.com/font" />
</ItemGroup>
```

Both take `License`, `TextFile` (read relative to the project), `Copyright` and
`Url`. The project's own licence heads the file, from `PackageLicenseExpression`
and the file `PackageLicenseFile` names among the packed `None` items, or
`ThirdPartyNoticesProjectLicenseFile`. `GenerateThirdPartyNotices` set to `false`
turns it all off.

#### Notices file format

A plugin built another way can write the file itself; the server reads the
`third-party-notices.json.br` beside the plugin's assembly, in the plugin's own
folder. It is UTF-8 JSON, Brotli compressed:

```json
{
  "version": 1,
  "project": { "id": "My.Plugin", "version": "1.0.0", "source": "project", "license": "MIT", "textKeys": ["<key>"] },
  "packages": [
    {
      "id": "Some.Package",
      "version": "1.2.3",
      "source": "nuget",
      "license": "Apache-2.0",
      "copyright": "© Someone",
      "url": "https://example.com",
      "textKeys": ["<key>", "<key>"]
    }
  ],
  "texts": { "<key>": "The licence text." }
}
```

- `version`: the format version, `1`.
- `project`: optional, the plugin's own licence, with `source` set to `project`.
- `packages`: every package or asset, sorted by `id`. `source` is `nuget`,
  `npm` or `asset`; `license` the SPDX expression as the package writes it;
  `version`, `license`, `copyright` and `url` may be left out; `textKeys` lists
  its texts in `texts`, the licence texts first.
- `texts`: each licence or notice text once, by key: the first 16 bytes of the
  SHA-256 of the text's UTF-8 bytes, as lowercase hex, after making its line
  endings `\n` and trimming trailing white space. The same text gets the same
  key in every file, so the server stores it once.

The targets write the members in this order, compact, with no time stamps, so
the same input always gives the same file.
