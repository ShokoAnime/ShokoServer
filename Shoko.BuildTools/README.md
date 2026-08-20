# Shoko.BuildTools

Build tool for Shoko plugins. Wraps MSBuild with assembly metadata injection,
auto-discovery of service interfaces, and manifest management.

## What it does

1. **Reads** the plugin's `manifest.json` (if supplied) for dependencies, tags, and identity.
2. **Scans source files** with Roslyn syntax trees to auto-detect Shoko service
   interface implementations (`IReleaseInfoProvider` → `release-provider`,
   `IHostedService` → `hosted-service`, etc.) and prepends them to the tags.
3. **Generates** a C# assembly metadata file with:
   - Runtime identifier, release date, source revision, release tag
   - Package identity (`PackageID`, `PackageName`, `PackageOverview`)
   - Dependencies serialized as `guid:versionRange[:true][,next]*` (`PackageDependencies`)
   - Tags as a single `PackageTags` attribute
4. **Loads** the `.csproj` in-memory via the MSBuild API, sets
   `EnableDynamicLoading=true`, injects the generated assembly info, restores
   and builds.
5. **Packs** the build output into a `.zip` archive.
6. **Computes** a SHA256 checksum of the zip.
7. **Updates** `manifest.json` — creates or updates a release entry with the
   archive URL, checksum, runtime, ABI version, and dependencies.
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
| `--channel <name>` | | Release channel for the manifest entry: `Stable`, `Dev` or `Debug`, case-insensitive. An unrecognised name is an error. See below for more info. |
| `--release-notes <text>` | | Release notes recorded on the manifest entry. See below. |
| `--release-notes-path <path>` | | Read the release notes from a file instead. Mutually exclusive with `--release-notes`. |

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

### Auto-detected tags

The source probe maps Shoko service interfaces to discovery tags:

| Interface | Tag |
|-----------|-----|
| `IReleaseInfoProvider` | `release-provider` |
| `IHashProvider` | `hash-provider` |
| `IRelocationProvider` | `relocation-provider` |
| `IImageCrossReferenceResolver` | `image-cross-reference-resolver` |
| `IManagedFolderIgnoreRule` | `managed-folder-ignore-rule` |
| `IResourceResolver` | `resource-resolver` |
| `ISupplementaryMetadataProvider` | `supplementary-metadata-provider` |
| `IHostedService` | `hosted-service` |
| `IPluginServiceRegistration` | `service-registration` |
| `IPluginApplicationRegistration` | `application-registration` |
| `IXxxProvider` (generic) | `xxx-provider` |

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
  "dependencies": [
    { "id": "<guid-of-dependency>", "version": ">=1.0.0" }
  ],
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
          "checksum": "sha256:<sha256>"
        }
      ]
    }
  ]
}
```

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

When using `shoko-build`, plugins no longer need the `PluginAssemblyVersion`
MSBuild target, `EnableDynamicLoading`, or `IncludeSourceRevisionInInformationalVersion`
in their `.csproj`. The tool handles all of that. A minimal plugin `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Library</OutputType>
    <Version>1.0.5</Version>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Shoko.Abstractions" Version="..." />
  </ItemGroup>
</Project>
```
