using System;
using System.IO;
using System.Linq;
using Shoko.Server.Plugin;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
/// A plugin exports its main assembly and the projects it was built with,
/// never the packages it ships.
/// </summary>
public sealed class PluginLoadContextTests : System.IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("plugin-load-context-").FullName;

    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void ExportsTheMainAssemblyAndProjects_NotPackages()
    {
        var main = Path.Join(_root, "Some.Plugin.dll");
        var deps = Path.Join(_root, "Some.Plugin.deps.json");
        File.WriteAllText(deps, """
            {
              "targets": {
                ".NETCoreApp,Version=v10.0": {
                  "Some.Plugin/1.0.0": { "runtime": { "Some.Plugin.dll": {} } },
                  "Some.Abstractions/1.0.0": { "runtime": { "Some.Abstractions.dll": {} } },
                  "Jint/4.17.0": { "runtime": { "lib/net8.0/Jint.dll": {} } }
                }
              },
              "libraries": {
                "Some.Plugin/1.0.0": { "type": "project" },
                "Some.Abstractions/1.0.0": { "type": "project" },
                "Jint/4.17.0": { "type": "package" }
              }
            }
            """);

        var exported = PluginLoadContext.ReadExportedAssemblies(main, deps);

        Assert.Equal(["Some.Abstractions", "Some.Plugin"], exported.Order());
    }

    [Fact]
    public void WithoutDepsJson_ExportsTheMainAssemblyOnly()
        => Assert.Equal(["Lone.Plugin"], PluginLoadContext.ReadExportedAssemblies(Path.Join(_root, "Lone.Plugin.dll"), Path.Join(_root, "missing.deps.json")));

    [Theory]
    [InlineData("1.10.0.0", "1.15.0.0", "1.15.0.0", true)]
    [InlineData("1.10.0.0", "1.15.0.0", "1.16.0.0", true)]
    [InlineData("1.10.0.0", "1.15.0.0", "1.12.0.0", false)]
    [InlineData("1.10.0.0", "1.10.0.0", "1.15.0.0", false)]
    [InlineData("1.10.0.0", "1.5.0.0", "1.15.0.0", false)]
    public void APluginUsesItsOwnCopyOnlyWhenItNeedsANewerOneThanTheServers(string host, string wanted, string own, bool usesOwn)
        => Assert.Equal(usesOwn, PluginLoadContext.PrefersOwnCopy(Version.Parse(host), Version.Parse(wanted), Version.Parse(own)));
}
