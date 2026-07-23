# Shoko.BuildTools

Build tool for Shoko plugins. Wraps MSBuild with assembly metadata injection,
auto-discovery of service interfaces, and manifest management.

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
| `--url <url>` | `-u` | Override the download URL for the archive in the manifest. Supports `{runtime}`, `{version}`, `{name}`, `{abstraction}` templates. If not provided, the URL is inferred from the git remote and the current tag. |
| `--output <path>` | `-o` | Output path for the packed `.zip` archive. Supports `{runtime}`, `{version}`, `{name}`, `{abstraction}` templates. If not provided, defaults to `{name}-v{version}-{abstraction}-{runtime}.zip` in the current directory. |

All other arguments are forwarded to MSBuild. Common forwarded args:

| Argument | Description |
|----------|-------------|
| `-c Release` | Build in Release configuration. |
| `-p:Version=1.0.5` | Override the version. |

### What it does

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
   `EnableDynamicLoading=true`, injects the generated assembly info, and builds.
5. **Packs** the build output into a `.zip` archive.
6. **Computes** a SHA256 checksum of the zip.
7. **Updates** `manifest.json` — creates or updates a release entry with the
   archive URL, checksum, runtime, ABI version, and dependencies.
8. **Prunes** old releases if `--prune` is set (per-channel or globally).

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

### Manifest format

See `manifest.schema.json` in this directory for the full JSON Schema.

```json
{
  "$schema": "../Shoko/Shoko.BuildTools/manifest.schema.json",
  "id": "72a6ff39-2bff-534c-9216-d03cd38e7346",
  "name": "Offline Importer",
  "overview": "Plugin responsible for importing releases based on file names.",
  "authors": "revam",
  "repository_url": "https://github.com/revam/dotnet-shoko-plugin-offline-importer",
  "tags": ["release-provider", "importer"],
  "dependencies": [
    { "id": "guid-of-dep", "version": ">=1.0.0" }
  ],
  "releases": [
    {
      "version": "1.0.5",
      "tag": "v1.0.5",
      "released_at": "2026-07-19T17:27:14Z",
      "channel": "Stable",
      "archives": [
        {
          "runtime": "linux-x64",
          "abstraction": "6.0.0",
          "url": "https://github.com/.../releases/download/v1.0.5/plugin.zip",
          "checksum": "sha256:abc123..."
        }
      ]
    }
  ]
}
```

### Examples

Basic build with manifest update:

```bash
shoko-build -c Release --manifest manifest.json
```

Build, pack zip to a custom path, with explicit download URL:

```bash
shoko-build -c Release \
  --manifest manifest.json \
  --url https://github.com/owner/repo/releases/download/v1.0.5/plugin.zip \
  --output ./dist/plugin.zip
```

Build with pruning (keep 5 most recent releases per channel):

```bash
shoko-build -c Release --manifest manifest.json --prune 5
```

Forward extra MSBuild properties:

```bash
shoko-build -c Release --manifest manifest.json -- -p:Version=1.0.5 -p:TargetFramework=net10.0
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
