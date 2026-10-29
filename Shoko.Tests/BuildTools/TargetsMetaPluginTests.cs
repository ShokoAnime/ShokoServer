using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;
using Moq;
using Shoko.BuildTools.Tasks;
using Xunit;

namespace Shoko.Tests.BuildTools;

/// <summary>
///   Covers how <c>Shoko.BuildTools.Targets</c> tells a meta plugin, one
///   whose only code is its generated plugin class, from one with code.
/// </summary>
public class TargetsMetaPluginTests : IDisposable
{
    static TargetsMetaPluginTests()
    {
        // The tasks run on MSBuild's own assemblies, which only an SDK holds.
        if (!MSBuildLocator.IsRegistered)
            MSBuildLocator.RegisterDefaults();
    }

    private readonly string _projectDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"shoko-targets-metadata-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
        => Directory.Delete(_projectDir, recursive: true);

    private static readonly string[] _generatedFolders = ["obj/", "obj\\Debug\\net10.0\\"];

    private const string GeneratedClass = "obj\\Debug\\net10.0\\Shoko.BuildTools.PluginClass.cs";

    private const string GlobalUsings = "obj/Debug/net10.0/Example.GlobalUsings.g.cs";

    [Theory]
    [InlineData(true, new[] { GeneratedClass, GlobalUsings }, true)]
    [InlineData(true, new[] { GeneratedClass, GlobalUsings, "Plugin.cs" }, false)]
    [InlineData(true, new[] { GeneratedClass, "../Shared/Helper.cs" }, false)]
    [InlineData(false, new[] { GlobalUsings }, false)]
    [InlineData(true, new string[0], true)]
    public void DetectsAPluginWithOnlyGeneratedCode(bool generateClass, string[] compileItems, bool expected)
    {
        Assert.Equal(expected, DetectMetaPlugin.IsMetaPlugin(generateClass, compileItems, _generatedFolders, [], _projectDir));
    }

    [Fact]
    public void ShokoBuildsAssemblyInfoIsNotTheProjectsOwnCode()
    {
        var assemblyInfo = Path.Combine(Path.GetTempPath(), "Shoko.Build.AssemblyInfo.any.cs");

        Assert.True(DetectMetaPlugin.IsMetaPlugin(true, [GeneratedClass, assemblyInfo], _generatedFolders, [assemblyInfo], _projectDir));
    }

    [Fact]
    public void StampsTheResult()
    {
        var outputPath = Path.Combine(_projectDir, "MetaPlugin.cs");
        var task = new DetectMetaPlugin
        {
            BuildEngine = new Mock<IBuildEngine>().Object,
            ProjectDir = _projectDir,
            OutputPath = outputPath,
            GenerateClass = true,
            GeneratedFolders = _generatedFolders,
        };

        Assert.True(task.Execute());
        Assert.Contains("[assembly: AssemblyMetadata(\"PackageIsMeta\", \"true\")]", File.ReadAllText(outputPath));
    }
}
