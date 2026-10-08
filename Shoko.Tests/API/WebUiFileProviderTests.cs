using System;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Moq;
using Shoko.Abstractions.Core.Services;
using Shoko.Server.API.FileProviders;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// The WebUI provider rewrites the built-in prefix in stylesheets and only
/// falls back to the index for client routes.
/// </summary>
public sealed class WebUiFileProviderTests : IDisposable
{
    private const string Stylesheet = "@font-face{src:url(/webui/assets/a.woff2)}a{background:url(\"/webui/assets/b.png\")}b{background:url('/webui/assets/c.png')}";

    private readonly string _root = Directory.CreateTempSubdirectory("webui-provider-").FullName;

    public WebUiFileProviderTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "assets"));
        File.WriteAllText(Path.Combine(_root, "index.html"), "<script>WEBUI_PREFIX='/webui';</script>");
        File.WriteAllText(Path.Combine(_root, "assets", "index.css"), Stylesheet);
    }

    public void Dispose() => Directory.Delete(_root, true);

    private IFileProvider Create(string prefix)
        => new WebUiFileProvider(Mock.Of<ISystemUpdateService>(), Mock.Of<IHttpContextAccessor>(), prefix, _root);

    private static string Read(IFileInfo file)
    {
        using var reader = new StreamReader(file.CreateReadStream());
        return reader.ReadToEnd();
    }

    [Theory]
    [InlineData("")]
    [InlineData("/foo")]
    public void Stylesheet_IsRewritten(string prefix)
    {
        var file = Create(prefix).GetFileInfo("/assets/index.css");

        Assert.Equal(Stylesheet.Replace("/webui/", $"{prefix}/"), Read(file));
        Assert.Equal(file.Length, file.CreateReadStream().Length);
    }

    [Fact]
    public void Stylesheet_IsUntouchedForBuiltInPrefix()
    {
        var file = Create("/webui").GetFileInfo("/assets/index.css");

        Assert.Equal(Path.Combine(_root, "assets", "index.css"), file.PhysicalPath);
        Assert.Equal(Stylesheet, Read(file));
    }

    [Theory]
    [InlineData("/assets/missing.woff2")]
    [InlineData("/assets/missing")]
    [InlineData("/missing.js")]
    public void MissingAsset_IsNotFound(string subpath)
        => Assert.False(Create("").GetFileInfo(subpath).Exists);

    [Fact]
    public void MissingRoute_FallsBackToIndex()
        => Assert.Contains("WEBUI_PREFIX='/foo';", Read(Create("/foo").GetFileInfo("/collection/series/1")));
}
