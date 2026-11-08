using System;
using System.IO;
using System.Linq;
using System.Text;
using Shoko.BuildTools.Tasks;
using Xunit;

namespace Shoko.Tests.BuildTools;

/// <summary>
///   Covers how <see cref="ThirdPartyNotices"/> collects the licences of the
///   packages a build ships into the notices file.
/// </summary>
public class ThirdPartyNoticesTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"shoko-notices-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
        => Directory.Delete(_root, recursive: true);

    private string Package(string id, string version, string nuspecMetadata, params (string Name, string Text)[] files)
    {
        var root = Directory.CreateDirectory(Path.Combine(_root, "packages", id.ToLowerInvariant(), version)).FullName;
        File.WriteAllText(Path.Combine(root, id.ToLowerInvariant() + ".nuspec"),
            $"<?xml version=\"1.0\"?><package xmlns=\"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd\"><metadata><id>{id}</id><version>{version}</version>{nuspecMetadata}</metadata></package>");
        foreach (var (name, text) in files)
        {
            var path = Path.Combine(root, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }
        return root;
    }

    [Fact]
    public void TheShippedPackagesAreTakenFromTheCopiedFiles_EachOnce()
    {
        var root = Path.Combine(_root, "packages", "some.package", "1.2.3");
        var packages = ThirdPartyNotices.ShippedPackages([
            (Path.Combine(root, "lib", "net8.0", "Some.Package.dll"), "Some.Package", "1.2.3", "lib/net8.0/Some.Package.dll"),
            (Path.Combine(root, "runtimes", "linux-x64", "native", "libsome.so"), "Some.Package", "1.2.3", "runtimes/linux-x64/native/libsome.so"),
            (Path.Combine(_root, "obj", "Project.dll"), null, null, null),
        ]);

        var package = Assert.Single(packages);
        Assert.Equal(("Some.Package", "1.2.3"), (package.ID, package.Version));
        Assert.Equal(root.Replace('\\', '/'), package.Root);
    }

    [Fact]
    public void ThePackagesTheHostShipsAreLeftOutWithWhatTheyDependOn()
    {
        const string assets = """
            {"targets":{"net10.0":{
              "Shoko.Abstractions/6.0.0":{"type":"package","dependencies":{"Newtonsoft.Json":"13.0.3"}},
              "Newtonsoft.Json/13.0.3":{"type":"package","dependencies":{"System.Tiny":"1.0.0"}},
              "System.Tiny/1.0.0":{"type":"package"},
              "Plugin.Own/2.0.0":{"type":"package","dependencies":{"Newtonsoft.Json":"13.0.3"}}
            }}}
            """;

        var closure = ThirdPartyNotices.HostClosure(assets, ["Shoko.Abstractions"]);

        Assert.Equal(["Newtonsoft.Json", "Shoko.Abstractions", "System.Tiny"], closure.Order(StringComparer.Ordinal));
        Assert.Empty(ThirdPartyNotices.HostClosure(assets, []));
    }

    [Fact]
    public void APackagesLicenceComesFromItsNuspecAndTheNoticeFilesItCarries()
    {
        var expression = Package("Expr.Package", "1.0.0",
            "<license type=\"expression\">Apache-2.0</license><licenseUrl>https://licenses.nuget.org/Apache-2.0</licenseUrl><copyright>© Someone</copyright><projectUrl>https://example.com</projectUrl>",
            ("NOTICE", "Apache notice\r\n"), ("LICENSE.dll", "not text"));
        var file = Package("File.Package", "1.0.0", "<license type=\"file\">docs/LICENSE.md</license>", ("docs/LICENSE.md", "The licence"), ("COPYING", "The other licence"));
        var urlOnly = Package("Url.Package", "1.0.0", "<licenseUrl>https://example.com/license</licenseUrl>");

        var a = ThirdPartyNotices.ReadPackage(expression, "Expr.Package");
        var b = ThirdPartyNotices.ReadPackage(file, "File.Package");
        var c = ThirdPartyNotices.ReadPackage(urlOnly, "Url.Package");

        Assert.Equal(("Apache-2.0", null, "© Someone", "https://example.com"), (a.License, a.LicenseUrl, a.Copyright, a.Url));
        Assert.Equal(["Apache notice"], a.NoticeTexts);
        Assert.Empty(a.LicenseTexts);
        Assert.True(a.IsKnown);
        Assert.Equal(["The licence", "The other licence"], b.LicenseTexts);
        Assert.True(b.IsKnown);
        Assert.Equal("https://example.com/license", c.LicenseUrl);
        Assert.False(c.IsKnown);
    }

    [Fact]
    public void AManualLicenceFillsInAPackage_TheVersionedOneFirst_AndWhatIsStillUnknownIsReported()
    {
        var unknown = new ThirdPartyNotices.PackageLicense(null, "https://example.com/license", null, null, [], []);
        ThirdPartyNotices.ShippedPackage Shipped(string id, string version) => new(id, version, null);

        var notices = ThirdPartyNotices.Build(
            new("My.Plugin", "1.0.0", "MIT", "The plugin's licence", null, null),
            [
                (Shipped("Versioned.Package", "2.0.0"), unknown),
                (Shipped("Fixed.Package", "1.0.0"), unknown),
                (Shipped("Unknown.Package", "3.0.0"), unknown),
                (Shipped("Known.Package", "1.0.0"), new("MIT", null, null, null, ["Same text"], ["Same text\n\n"])),
            ],
            [
                new("Versioned.Package", null, "BSD-3-Clause", null, null, null),
                new("Versioned.Package", "2.0.0", "MIT", "Versioned text", null, null),
                new("Fixed.Package", null, "Apache-2.0", null, null, null),
            ],
            [new("Some Font", "2.0", "OFL-1.1", "Font licence", null, "https://example.com/font")],
            out var missing
        );

        Assert.Equal(["Unknown.Package@3.0.0"], missing);
        Assert.Equal(["Fixed.Package", "Known.Package", "Some Font", "Unknown.Package", "Versioned.Package"], notices.Packages.Select(entry => entry.ID));
        var versioned = notices.Packages.Single(entry => entry.ID == "Versioned.Package");
        Assert.Equal("MIT", versioned.License);
        Assert.Equal("Versioned text", notices.Texts[Assert.Single(versioned.TextKeys)]);
        Assert.Single(notices.Packages.Single(entry => entry.ID == "Known.Package").TextKeys);
        Assert.Equal("asset", notices.Packages.Single(entry => entry.ID == "Some Font").Source);
        Assert.Equal("https://example.com/license", notices.Packages.Single(entry => entry.ID == "Unknown.Package").Url);
        Assert.Equal(("My.Plugin", "project", "MIT"), (notices.Project!.ID, notices.Project.Source, notices.Project.License));
    }

    [Fact]
    public void TheFileIsTheSameForTheSameNotices_AndAPackageIsReadFromTheCacheOnceKnown()
    {
        var root = Package("Cached.Package", "1.0.0", "<license type=\"expression\">MIT</license>", ("LICENSE", "Cached licence"));
        var cache = Path.Combine(_root, "cache");
        var package = new ThirdPartyNotices.ShippedPackage("Cached.Package", "1.0.0", root);

        var first = ThirdPartyNotices.ReadPackageCached(cache, package)!;
        Directory.Delete(root, recursive: true);
        var second = ThirdPartyNotices.ReadPackageCached(cache, package)!;
        var notices = ThirdPartyNotices.Build(null, [(package, second)], [], [], out _);
        var json = Encoding.UTF8.GetString(ThirdPartyNotices.ToJson(notices));

        Assert.Equal(first.LicenseTexts, second.LicenseTexts);
        Assert.Equal(ThirdPartyNotices.ToJson(notices), ThirdPartyNotices.ToJson(ThirdPartyNotices.Build(null, [(package, second)], [], [], out _)));
        Assert.StartsWith("{\"version\":1,\"packages\":[{\"id\":\"Cached.Package\",\"version\":\"1.0.0\",\"source\":\"nuget\",\"license\":\"MIT\",\"textKeys\":[\"", json);
        Assert.DoesNotContain("\"project\"", json);
        Assert.Contains($"\"{ThirdPartyNotices.KeyOf("Cached licence")}\":\"Cached licence\"", json);
    }
}
