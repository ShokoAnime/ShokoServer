using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Plugin;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
///   Covers how the core reads a plugin's namespace, and fails the plugins
///   whose namespace is not valid or is claimed twice.
/// </summary>
public class PluginNamespaceTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), $"shoko-plugin-namespace-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
        => Directory.Delete(_root, recursive: true);

    #region Reading

    /// <summary>
    ///   Compiles a plugin stamped the way the build tools stamp one.
    /// </summary>
    private string CompilePlugin(string assemblyName, string? pluginNamespace)
    {
        var id = Guid.NewGuid();
        var namespaceAttribute = pluginNamespace is null ? string.Empty : $"[assembly: System.Reflection.AssemblyMetadata(\"PackageNamespace\", \"{pluginNamespace}\")]";
        var source = $$"""
            using System;
            using Shoko.Abstractions.Plugin;

            [assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
            [assembly: System.Reflection.AssemblyMetadata("PackageID", "{{id}}")]
            [assembly: System.Reflection.AssemblyMetadata("PackageName", "{{assemblyName}}")]
            {{namespaceAttribute}}

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

    private static PluginManager.InternalPluginInfo Read(string dllPath)
    {
        var manager = TestPluginManager.Create();
        var settingsChanged = false;
        var info = manager.LoadInternalPluginInfo(null, [dllPath], false, new ServerSettings(), ref settingsChanged);
        Assert.NotNull(info);
        return info;
    }

    [Fact]
    public void ReadsTheStampedNamespace()
    {
        var info = Read(CompilePlugin("NamespacedPlugin", "example-name"));

        Assert.Equal("example-name", info.Namespace);
        Assert.Null(info.FailureReason);
    }

    [Fact]
    public void APluginWithoutANamespaceLoads()
    {
        var info = Read(CompilePlugin("PlainPlugin", null));

        Assert.Null(info.Namespace);
        Assert.Null(info.FailureReason);
        Assert.True(info.CanLoad);
    }

    [Fact]
    public void APluginBuiltWithTheCurrentTargetsLoads()
    {
        // The legacy Plex plugin is built with the released targets package, which stamps no namespace.
        var info = Read(Path.Combine(AppContext.BaseDirectory, "Shoko.Plugin.LegacyPlex.dll"));

        Assert.Equal(new Shoko.Plugin.LegacyPlex.Plugin().ID, info.ID);
        Assert.Null(info.Namespace);
        Assert.Null(info.FailureReason);
        Assert.True(info.CanLoad);
    }

    [Theory]
    [InlineData("Example", "kebab-case")]
    [InlineData("x", "2 to 32")]
    [InlineData("shoko", "reserved")]
    [InlineData("core", "reserved")]
    [InlineData("plugin", "reserved")]
    public void ABadNamespaceFailsThePlugin(string value, string reason)
    {
        var info = Read(CompilePlugin("BadlyNamespacedPlugin", value));

        Assert.Null(info.Namespace);
        Assert.NotNull(info.FailureReason);
        Assert.Contains($"\"{value}\"", info.FailureReason);
        Assert.Contains(reason, info.FailureReason);
    }

    [Fact]
    public void ABadNamespaceFailsTheStart()
    {
        var manager = TestPluginManager.Create();
        var info = Read(CompilePlugin("BadlyNamespacedPlugin", "core"));
        info.IsEnabled = true;

        var (toLoad, failureReason, _) = manager.PickVersionToLoad([info], new Dictionary<string, string>());

        Assert.Same(info, toLoad);
        Assert.Equal(info.FailureReason, failureReason);
    }

    #endregion

    #region Uniqueness

    [Fact]
    public void TwoPluginsClaimingOneNamespaceBothFail()
    {
        var manager = TestPluginManager.Create();
        var first = PluginTestDoubles.PickedPluginInfo(Guid.NewGuid(), "First", "example");
        var second = PluginTestDoubles.PickedPluginInfo(Guid.NewGuid(), "Second", "example");
        var other = PluginTestDoubles.PickedPluginInfo(Guid.NewGuid(), "Other", "other");
        var plain = PluginTestDoubles.PickedPluginInfo(Guid.NewGuid(), "Plain", null);
        TestPluginManager.Plugins(manager).AddRange([first, second, other, plain]);

        manager.FailPluginsClaimingTheSameNamespace();

        Assert.True(first.HasFailed);
        Assert.Contains("\"example\"", first.FailureReason);
        Assert.Contains($"\"Second\" ({second.ID})", first.FailureReason);
        Assert.False(first.CanLoad);
        Assert.True(second.HasFailed);
        Assert.Contains($"\"First\" ({first.ID})", second.FailureReason);
        Assert.False(other.HasFailed);
        Assert.False(plain.HasFailed);
        Assert.Equal([first, second], manager.GetFailedPlugins());
    }

    [Fact]
    public void ADisabledPluginClaimingTheNamespaceIsLeftAlone()
    {
        var manager = TestPluginManager.Create();
        var enabled = PluginTestDoubles.PickedPluginInfo(Guid.NewGuid(), "Enabled", "example");
        var disabled = PluginTestDoubles.PickedPluginInfo(Guid.NewGuid(), "Disabled", "example");
        disabled.IsEnabled = false;
        TestPluginManager.Plugins(manager).AddRange([enabled, disabled]);

        manager.FailPluginsClaimingTheSameNamespace();

        Assert.False(enabled.HasFailed);
        Assert.False(disabled.HasFailed);
    }

    #endregion
}
