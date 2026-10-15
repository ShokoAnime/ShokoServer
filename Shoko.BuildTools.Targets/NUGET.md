# Shoko.BuildTools.Targets

MSBuild targets for [Shoko Server](https://shokoanime.com) plugin projects. On every build they stamp the assembly metadata the server reads from a plugin: its identity (from `manifest.json` or the source), the runtime identifier, the release channel and the source revision.

## Getting started

```xml
<PackageReference Include="Shoko.BuildTools.Targets" Version="..." PrivateAssets="All" />
```

Nothing else is needed; the targets run as part of `dotnet build`. To also pack a release archive and update a release manifest, use the [Shoko.BuildTools](https://www.nuget.org/packages/Shoko.BuildTools) tool.

## Documentation

- [Build tools guide](https://github.com/ShokoAnime/ShokoServer/blob/master/Shoko.BuildTools/README.md)
- [Writing a Shoko plugin](https://github.com/ShokoAnime/ShokoServer/blob/master/Shoko.Abstractions/README.md)
- [Issues](https://github.com/ShokoAnime/ShokoServer/issues) and [Discord](https://discord.gg/shokoanime)
