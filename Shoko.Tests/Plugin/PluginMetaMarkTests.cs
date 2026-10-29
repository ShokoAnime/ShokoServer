using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
///   Covers how the core reads the mark the build tools stamp a metadata
///   plugin with, one that only brings in its dependencies.
/// </summary>
public class PluginMetaMarkTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), $"shoko-plugin-metadata-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
        => Directory.Delete(_root, recursive: true);

    private string CompilePlugin(string assemblyName, string? isMeta)
    {
        var id = Guid.NewGuid();
        var stamp = isMeta is null ? string.Empty : $"[assembly: System.Reflection.AssemblyMetadata(\"PackageIsMeta\", \"{isMeta}\")]";
        var source = $$"""
            using System;
            using Shoko.Abstractions.Plugin;

            [assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
            [assembly: System.Reflection.AssemblyMetadata("PackageID", "{{id}}")]
            {{stamp}}

            namespace {{assemblyName}};

            public class Plugin : IPlugin
            {
                public Guid ID => new("{{id}}");

                public string Name => "{{assemblyName}}";
            }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Append(typeof(IPlugin).Assembly.Location)
            .Distinct(StringComparer.Ordinal)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var path = Path.Join(_root, $"{assemblyName}.dll");
        using (var stream = File.Create(path))
        {
            var result = compilation.Emit(stream);
            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        }

        return path;
    }

    [Theory]
    [InlineData("MetaPlugin", "true", true)]
    [InlineData("CodePlugin", "false", false)]
    [InlineData("UnmarkedPlugin", null, null)]
    public void ReadsTheStampedMark(string assemblyName, string? stamped, bool? expected)
    {
        var settingsChanged = false;
        var info = TestPluginManager.Create().LoadInternalPluginInfo(null, [CompilePlugin(assemblyName, stamped)], false, new ServerSettings(), ref settingsChanged);

        Assert.NotNull(info);
        Assert.Equal(expected, info.IsMeta);
    }
}
