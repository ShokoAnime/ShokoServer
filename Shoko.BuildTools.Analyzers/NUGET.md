# Shoko.BuildTools.Analyzers

Compile-time checks for [Shoko Server](https://shokoanime.com) plugin authors. They cover the classes the server describes as forms: configurations (`IConfiguration`) and the parameters of executable actions (`IExecutableAction`). Each rule reports a shape the server's schema generator would refuse at start-up, so the mistake is caught where it is written.

## Getting started

```xml
<PackageReference Include="Shoko.BuildTools.Analyzers" Version="..." PrivateAssets="All" />
```

Every rule is an error in the `Shoko.Configuration` category.

## Documentation

- [Rules](https://github.com/ShokoAnime/ShokoServer/blob/master/Shoko.BuildTools.Analyzers/README.md), each with the message the server gives for the same mistake
- [Writing a Shoko plugin](https://github.com/ShokoAnime/ShokoServer/blob/master/Shoko.Abstractions/README.md)
- [Issues](https://github.com/ShokoAnime/ShokoServer/issues) and [Discord](https://discord.gg/vpeHDsg)
