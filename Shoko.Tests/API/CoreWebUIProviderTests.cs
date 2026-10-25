using System;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Plugin;
using Shoko.Server.API.FileProviders;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// The Shoko Web UI's files: the index for its own routes, nothing for a
/// missing asset.
/// </summary>
public sealed class CoreWebUIProviderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("webui-provider-").FullName;

    private readonly CoreWebUIProvider _provider;

    public CoreWebUIProviderTests()
    {
        var webPath = Path.Combine(_root, "webui");
        Directory.CreateDirectory(Path.Combine(webPath, "assets"));
        File.WriteAllText(Path.Combine(webPath, "index.html"), "<script>WEBUI_PREFIX='/webui';</script>");
        File.WriteAllText(Path.Combine(webPath, "assets", "index.css"), "a{}");
        _provider = Create(_root);
    }

    public void Dispose() => Directory.Delete(_root, true);

    private CoreWebUIProvider Create(string dataPath)
    {
        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.SetupGet(paths => paths.DataPath).Returns(dataPath);
        applicationPaths.SetupGet(paths => paths.ApplicationPath).Returns(Path.Combine(_root, "app"));
        var httpClientFactory = Mock.Of<IHttpClientFactory>();
        return new(
            NullLogger<CoreWebUIProvider>.Instance,
            Mock.Of<ISettingsProvider>(),
            applicationPaths.Object,
            httpClientFactory,
            new(NullLogger<ReleaseManifestReader>.Instance, httpClientFactory, applicationPaths.Object)
        );
    }

    private static string Read(IFileInfo file)
    {
        using var reader = new StreamReader(file.CreateReadStream());
        return reader.ReadToEnd();
    }

    [Fact]
    public void Asset_IsServed()
        => Assert.Equal("a{}", Read(_provider.GetFileInfo("/assets/index.css")));

    [Theory]
    [InlineData("/assets/missing.woff2")]
    [InlineData("/assets/missing")]
    [InlineData("/missing.js")]
    public void MissingAsset_IsNotFound(string subpath)
        => Assert.False(_provider.GetFileInfo(subpath).Exists);

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("/collection/series/1")]
    public void Route_FallsBackToIndex(string subpath)
        => Assert.Contains("WEBUI_PREFIX='/webui';", Read(_provider.GetFileInfo(subpath)));

    [Fact]
    public void NewInstallation_StartsWithTheShippedFiles()
    {
        Directory.CreateDirectory(Path.Combine(_root, "app", "webui"));
        File.WriteAllText(Path.Combine(_root, "app", "webui", "index.html"), "<p>install-web-ui</p>");

        var provider = Create(Path.Combine(_root, "new"));

        Assert.Contains("install-web-ui", Read(provider.GetFileInfo("/")));
        Assert.Null(provider.CurrentRelease);
    }
}
