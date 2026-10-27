using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;
using Moq;
using Shoko.BuildTools.Tasks;
using Xunit;

namespace Shoko.Tests.BuildTools;

/// <summary>
///   Covers how <c>Shoko.BuildTools.Targets</c> reads, checks and stamps a
///   plugin's namespace.
/// </summary>
public class TargetsPluginNamespaceTests : IDisposable
{
    static TargetsPluginNamespaceTests()
    {
        // The tasks run on MSBuild's own assemblies, which only an SDK holds.
        if (!MSBuildLocator.IsRegistered)
            MSBuildLocator.RegisterDefaults();
    }

    private readonly string _projectDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"shoko-targets-namespace-{Guid.NewGuid():N}")).FullName;

    private readonly List<string> _errors = [];

    public void Dispose()
        => Directory.Delete(_projectDir, recursive: true);

    private string OutputPath => Path.Combine(_projectDir, "ManifestInfo.cs");

    private IBuildEngine BuildEngine()
    {
        var engine = new Mock<IBuildEngine>();
        engine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>())).Callback<BuildErrorEventArgs>(e => _errors.Add(e.Message!));
        return engine.Object;
    }

    private bool RunManifestTask(string? manifestNamespace, string? property)
    {
        var namespaceField = manifestNamespace is null ? string.Empty : $", \"namespace\": \"{manifestNamespace}\"";
        File.WriteAllText(
            Path.Combine(_projectDir, "manifest.json"),
            $$"""{ "id": "0f8a1c2e-1111-2222-3333-444455556666", "name": "Example"{{namespaceField}} }""");
        var task = new ReadPluginManifest
        {
            BuildEngine = BuildEngine(),
            ProjectDir = _projectDir,
            OutputPath = OutputPath,
            PluginNamespace = property,
        };
        return task.Execute();
    }

    [Fact]
    public void StampsTheManifestNamespace()
    {
        Assert.True(RunManifestTask("example", null));

        Assert.Contains("[assembly: AssemblyMetadata(\"PackageNamespace\", \"example\")]", File.ReadAllText(OutputPath));
    }

    [Fact]
    public void ThePropertyWinsOverTheManifest()
    {
        Assert.True(RunManifestTask("example", " other-name "));

        var output = File.ReadAllText(OutputPath);
        Assert.Contains("[assembly: AssemblyMetadata(\"PackageNamespace\", \"other-name\")]", output);
        Assert.DoesNotContain("\"example\"", output);
    }

    [Fact]
    public void StampsNothingWithoutANamespace()
    {
        Assert.True(RunManifestTask(null, null));

        Assert.DoesNotContain("PackageNamespace", File.ReadAllText(OutputPath));
    }

    [Theory]
    [InlineData("Example", "kebab-case")]
    [InlineData("a", "2 to 32")]
    [InlineData("example--name", "kebab-case")]
    [InlineData("1example", "kebab-case")]
    [InlineData("an-overly-long-namespace-for-a-plugin", "2 to 32")]
    [InlineData("shoko", "reserved")]
    [InlineData("core", "reserved")]
    [InlineData("plugin", "reserved")]
    public void FailsTheBuildOnABadNamespace(string value, string reason)
    {
        Assert.False(RunManifestTask(null, value));

        var error = Assert.Single(_errors);
        Assert.Contains($"'{value}'", error);
        Assert.Contains("PluginNamespace", error);
        Assert.Contains(reason, error);
        Assert.False(File.Exists(OutputPath));
    }

    [Fact]
    public void NamesTheManifestABadNamespaceCameFrom()
    {
        Assert.False(RunManifestTask("Core", null));

        Assert.Contains("manifest.json", Assert.Single(_errors));
    }

    [Fact]
    public void TheSourceReaderStampsTheProperty()
    {
        var task = new ReadPluginIdentityFromSource
        {
            BuildEngine = BuildEngine(),
            ProjectDir = _projectDir,
            OutputPath = OutputPath,
            PluginID = "0f8a1c2e-1111-2222-3333-444455556666",
            PluginNamespace = "example",
        };

        Assert.True(task.Execute());
        Assert.Contains("[assembly: AssemblyMetadata(\"PackageNamespace\", \"example\")]", File.ReadAllText(OutputPath));
    }
}
