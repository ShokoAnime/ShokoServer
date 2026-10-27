using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;
using Moq;
using Shoko.BuildTools.Tasks;
using Xunit;

namespace Shoko.Tests.BuildTools;

/// <summary>
///   Covers that <c>Shoko.BuildTools.Targets</c> leaves a generated file
///   alone when its text is unchanged, so an unchanged plugin is not
///   recompiled.
/// </summary>
public class TargetsGeneratedFileTests : IDisposable
{
    static TargetsGeneratedFileTests()
    {
        // The tasks run on MSBuild's own assemblies, which only an SDK holds.
        if (!MSBuildLocator.IsRegistered)
            MSBuildLocator.RegisterDefaults();
    }

    private readonly string _projectDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"shoko-targets-generated-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
        => Directory.Delete(_projectDir, recursive: true);

    private string OutputPath => Path.Combine(_projectDir, "ManifestInfo.cs");

    private bool Run(string name)
    {
        File.WriteAllText(
            Path.Combine(_projectDir, "manifest.json"),
            $$"""{ "id": "0f8a1c2e-1111-2222-3333-444455556666", "name": "{{name}}" }""");
        return new ReadPluginManifest
        {
            BuildEngine = new Mock<IBuildEngine>().Object,
            ProjectDir = _projectDir,
            OutputPath = OutputPath,
        }.Execute();
    }

    [Fact]
    public void AnUnchangedFileIsNotWrittenAgain()
    {
        Assert.True(Run("Example"));
        var past = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(OutputPath, past);

        Assert.True(Run("Example"));
        Assert.Equal(past, File.GetLastWriteTimeUtc(OutputPath));

        Assert.True(Run("Renamed"));
        Assert.NotEqual(past, File.GetLastWriteTimeUtc(OutputPath));
        Assert.Contains("Renamed", File.ReadAllText(OutputPath));
    }
}
