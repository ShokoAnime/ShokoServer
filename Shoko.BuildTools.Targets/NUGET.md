# Shoko.BuildTools.Targets

MSBuild targets for [Shoko Server](https://shokoanime.com) plugin projects. On every build they stamp the assembly metadata the server reads from a plugin: its identity (from the project, `manifest.json` or the source), its dependencies (from the project), the runtime identifier, the release channel and the source revision. They also turn on `EnableDynamicLoading` unless the project sets it, so the plugin's own NuGet dependencies are copied into its output as the server expects; a project that is not a plugin sets it to `false`.

## Getting started

```xml
<PackageReference Include="Shoko.BuildTools.Targets" Version="..." PrivateAssets="All" />
```

Nothing else is needed; the targets run as part of `dotnet build`. To also pack a release archive and update a release manifest, use the [Shoko.BuildTools](https://www.nuget.org/packages/Shoko.BuildTools) tool.

## Plugin metadata

The plugin's identity and tags come from its `manifest.json`, or from its source without one, unless the project states them as `PluginID`, `PluginName`, `PluginOverview`, `PluginNamespace` and `PluginTags` properties. The namespace is optional: a short, unique kebab-case name of 2 to 32 characters (`^[a-z][a-z0-9]*(-[a-z0-9]+)*$`) the plugin reserves for itself, which other plugins and conventions may key off, stamped as `PackageNamespace`; `shoko`, `core` and `plugin` are reserved, and a bad one stops the build. Its dependencies, the plugins the server loads before it, belong in the project, one `PluginDependency` item per plugin:

```xml
<ItemGroup>
  <PluginDependency Include="0f8a1c2e-1111-2222-3333-444455556666" Version="^1.2.0" />
  <PluginDependency Include="7d3b9f40-aaaa-bbbb-cccc-ddddeeeeffff" Version=">=2.0" Optional="true" />
</ItemGroup>
```

`Include` is the plugin's ID, `Version` a range (`>=1.0.0`, `^1.0.0`, `~1.0.0` or `1.0.0`), and `Optional` `true` or `false`, `false` when left out. The `PluginDependencies` property, the same list as one string, replaces the items. A manifest's top-level `dependencies` are not read. A bad item stops the build, naming it. See [Plugin metadata](https://github.com/ShokoAnime/ShokoServer/blob/master/Shoko.BuildTools/README.md#plugin-metadata) and [Dependencies](https://github.com/ShokoAnime/ShokoServer/blob/master/Shoko.BuildTools/README.md#dependencies) in the guide.

## Generated plugin class and images

A project setting `PluginGenerateClass` to `true` gets its `IPlugin` class generated: a `partial` class `Plugin` in its `RootNamespace` (or `PluginClassName`), whose ID, name and description come from the stamped identity. Add a main configuration or behaviour in a partial class of your own; with no other code, it is a meta plugin that only brings in its `PluginDependency` items. The targets detect that, with no property to set, and stamp `PackageIsMeta` as `true` or `false`, which the server shows as `IsMeta`. `PluginIcon` and `PluginThumbnail` name the image files to embed for the server to show. See [Generated plugin class](https://github.com/ShokoAnime/ShokoServer/blob/master/Shoko.BuildTools/README.md#generated-plugin-class) and [Plugin images](https://github.com/ShokoAnime/ShokoServer/blob/master/Shoko.BuildTools/README.md#plugin-images) in the guide.

## Documentation

- [Build tools guide](https://github.com/ShokoAnime/ShokoServer/blob/master/Shoko.BuildTools/README.md)
- [Writing a Shoko plugin](https://github.com/ShokoAnime/ShokoServer/blob/master/Shoko.Abstractions/README.md)
- [Issues](https://github.com/ShokoAnime/ShokoServer/issues) and [Discord](https://discord.gg/shokoanime)
