using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Moq;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
///   A plugin whose public types use an assembly only another plugin ships,
///   such as a contract of a plugin it depends on, is read with that
///   plugin's copy.
/// </summary>
public class PluginReaderSharedAssemblyTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), $"shoko-plugin-reader-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
        => Directory.Delete(_root, recursive: true);

    [Fact]
    public void ReadsAPluginUsingAContractAnotherPluginExports()
    {
        var plugins = Directory.CreateDirectory(Path.Join(_root, "plugins")).FullName;
        var contract = Compile(Directory.CreateDirectory(Path.Join(plugins, "Provider")).FullName, "Provider.Contract", "namespace Provider.Contract; public enum Kind { One, Two }");
        File.WriteAllText(Path.Join(plugins, "Provider", "Provider.deps.json"), """
            {
              "targets": { ".NETCoreApp,Version=v10.0": { "Provider.Contract/1.0.0": { "runtime": { "Provider.Contract.dll": {} } } } },
              "libraries": { "Provider.Contract/1.0.0": { "type": "project" } }
            }
            """);

        var consumer = Directory.CreateDirectory(Path.Join(plugins, "Consumer")).FullName;
        var id = Guid.NewGuid();
        var dll = Compile(consumer, "Consumer", $$"""
            using System;
            using Shoko.Abstractions.Plugin;

            [assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
            [assembly: System.Reflection.AssemblyMetadata("PackageID", "{{id}}")]

            namespace Consumer;

            public class Plugin : IPlugin
            {
                public Guid ID => new("{{id}}");

                public string Name => "Consumer";
            }

            public class Request
            {
                public Provider.Contract.Kind Kind { get; init; }
            }
            """, contract);

        Assert.True(Read(consumer, dll)?.CanLoad);

        Directory.Delete(Path.Join(plugins, "Provider"), recursive: true);
        Assert.False(Read(consumer, dll)?.CanLoad);
    }

    private Server.Plugin.PluginManager.InternalPluginInfo? Read(string directory, string dll)
    {
        var paths = Mock.Of<IApplicationPaths>(paths => paths.ApplicationPath == Path.Join(_root, "app") && paths.PluginsPath == Path.Join(_root, "plugins"));
        var settingsChanged = false;
        return TestPluginManager.Create(paths).LoadInternalPluginInfo(directory, [dll], false, new ServerSettings(), ref settingsChanged);
    }

    private static string Compile(string directory, string assemblyName, string source, params string[] extraReferences)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Append(typeof(IPlugin).Assembly.Location)
            .Concat(extraReferences)
            .Distinct(StringComparer.Ordinal)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var path = Path.Join(directory, $"{assemblyName}.dll");
        using (var stream = File.Create(path))
        {
            var result = compilation.Emit(stream);
            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        }

        return path;
    }
}
