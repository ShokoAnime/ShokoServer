using System;
using System.IO;
using Shoko.BuildTools;
using Xunit;

namespace Shoko.Tests.BuildTools;

/// <summary>
///   Covers the namespace rules <c>shoko-build</c> checks and the manifest
///   field it reads them from.
/// </summary>
public class BuildToolsPluginNamespaceTests
{
    [Theory]
    [InlineData("ab")]
    [InlineData("example")]
    [InlineData("example-name2")]
    [InlineData("a1-b2-c3")]
    [InlineData("abcdefghijklmnopqrstuvwxyz-abcde")]
    public void AcceptsKebabCase(string value)
        => Assert.Null(PluginNamespaceRules.Validate(value));

    [Theory]
    [InlineData("a")]
    [InlineData("abcdefghijklmnopqrstuvwxyz-abcdef")]
    [InlineData("Example")]
    [InlineData("1example")]
    [InlineData("-example")]
    [InlineData("example-")]
    [InlineData("example--name")]
    [InlineData("example_name")]
    [InlineData("example.name")]
    [InlineData("shoko")]
    [InlineData("core")]
    [InlineData("plugin")]
    public void RefusesTheRest(string value)
        => Assert.NotNull(PluginNamespaceRules.Validate(value));

    [Fact]
    public void ReadsAndWritesTheManifestNamespace()
    {
        var path = Path.Combine(Path.GetTempPath(), $"shoko-manifest-namespace-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{ "id": "0f8a1c2e-1111-2222-3333-444455556666", "name": "Example", "namespace": "example" }""");

            var manifest = ManifestManager.Load(path);
            Assert.Equal("example", manifest.Namespace);

            ManifestManager.Save(manifest, path);
            Assert.Contains("\"namespace\": \"example\"", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
