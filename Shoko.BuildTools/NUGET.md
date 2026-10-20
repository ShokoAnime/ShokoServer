# Shoko.BuildTools

The `shoko-build` command: builds and packs a [Shoko Server](https://shokoanime.com) plugin. It wraps MSBuild, stamps the assembly metadata the server reads from a plugin (identity, runtime, release channel, source revision), detects which service contracts the plugin implements, and writes or updates the release manifest a plugin repository serves.

## Getting started

```
dotnet tool install --global Shoko.BuildTools
shoko-build -c Release --channel Stable --tag v1.0.0 --manifest manifest.json
```

For the metadata alone, on every ordinary `dotnet build`, reference [Shoko.BuildTools.Targets](https://www.nuget.org/packages/Shoko.BuildTools.Targets) from the plugin project instead.

## Plugin metadata

The plugin's identity and tags come from the command line, then the project, then the manifest. Its dependencies come from the command line or the project alone, never the manifest. Its dependencies, the plugins the server loads before it, belong in the project, one `PluginDependency` item per plugin:

```xml
<ItemGroup>
  <PluginDependency Include="0f8a1c2e-1111-2222-3333-444455556666" Version="^1.2.0" />
  <PluginDependency Include="7d3b9f40-aaaa-bbbb-cccc-ddddeeeeffff" Version=">=2.0" Optional="true" />
</ItemGroup>
```

They are stamped into the assembly and recorded in the release entry the build adds. `--dependencies` and the `PluginDependencies` property take the same list as one string and replace the items. The other fields are `--id`, `--name`, `--overview` and `--tags`, or the `PluginID`, `PluginName`, `PluginOverview` and `PluginTags` properties. See [Plugin metadata](https://github.com/ShokoAnime/ShokoServer/blob/master/Shoko.BuildTools/README.md#plugin-metadata) and [Dependencies](https://github.com/ShokoAnime/ShokoServer/blob/master/Shoko.BuildTools/README.md#dependencies) in the guide.

## Documentation

- [Full guide](https://github.com/ShokoAnime/ShokoServer/blob/master/Shoko.BuildTools/README.md): options, plugin metadata and dependencies, the manifest format
- [Plugin template](https://github.com/ShokoAnime/Plugin-Template), with a release workflow using this tool
- [Issues](https://github.com/ShokoAnime/ShokoServer/issues) and [Discord](https://discord.gg/shokoanime)
