# Shoko.Abstractions

The contract between [Shoko Server](https://shokoanime.com) and the plugins that extend it. Everything in this package is an interface, a model or an enum: the server implements them, a plugin consumes them, and neither side references the other's concrete types.

## Getting started

Reference the package without its runtime assets, since the server supplies the assembly:

```xml
<PackageReference Include="Shoko.Abstractions" Version="..." ExcludeAssets="runtime" />
```

Implement `IPlugin`, build, and drop the output in the server's `plugins` folder. The [plugin template](https://github.com/ShokoAnime/Plugin-Template) is the quickest start, and [Shoko.BuildTools.Targets](https://www.nuget.org/packages/Shoko.BuildTools.Targets) stamps the metadata the server reads from a plugin.

## Versions

A plugin runs on the server whose abstractions it was built for. While 6.0 is in alpha every release may change the contract, so build against the version your server ships.

## Documentation

- [Writing a Shoko plugin](https://github.com/ShokoAnime/ShokoServer/blob/master/Shoko.Abstractions/README.md), with a page per contract and service
- [Sample plugins](https://github.com/ShokoAnime/SamplePlugins)
- [Issues](https://github.com/ShokoAnime/ShokoServer/issues) and [Discord](https://discord.gg/vpeHDsg)
