# Shoko.BuildTools

The `shoko-build` command: builds and packs a [Shoko Server](https://shokoanime.com) plugin. It wraps MSBuild, stamps the assembly metadata the server reads from a plugin (identity, runtime, release channel, source revision), detects which service contracts the plugin implements, and writes or updates the release manifest a plugin repository serves.

## Getting started

```
dotnet tool install --global Shoko.BuildTools
shoko-build -c Release --channel Stable --tag v1.0.0 --manifest manifest.json
```

For the metadata alone, on every ordinary `dotnet build`, reference [Shoko.BuildTools.Targets](https://www.nuget.org/packages/Shoko.BuildTools.Targets) from the plugin project instead.

## Documentation

- [Full guide](https://github.com/ShokoAnime/ShokoServer/blob/master/Shoko.BuildTools/README.md): options, plugin metadata, the manifest format
- [Plugin template](https://github.com/ShokoAnime/Plugin-Template), with a release workflow using this tool
- [Issues](https://github.com/ShokoAnime/ShokoServer/issues) and [Discord](https://discord.gg/shokoanime)
