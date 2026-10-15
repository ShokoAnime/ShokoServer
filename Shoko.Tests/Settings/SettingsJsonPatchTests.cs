using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Settings;

public class SettingsJsonPatchTests
{
    private const string ImageLanguageOrder = "/Metadata/SourceDefaults/Images/ImageLanguageOrder";

    [Theory]
    [InlineData(
        "replace",
        ImageLanguageOrder + "/0",
        "fr",
        new[] { "fr", "x-main", "en" },
        new[] { TitleLanguage.French, TitleLanguage.Main, TitleLanguage.English }
    )]
    [InlineData(
        "remove",
        ImageLanguageOrder + "/0",
        null,
        new[] { "x-main", "en" },
        new[] { TitleLanguage.Main, TitleLanguage.English }
    )]
    [InlineData(
        "add",
        ImageLanguageOrder + "/-",
        "de",
        new[] { "none", "x-main", "en", "de" },
        new[] { TitleLanguage.None, TitleLanguage.Main, TitleLanguage.English, TitleLanguage.German }
    )]
    public void PerItemOps_OnMetadataSourceImageLanguageOrder_MutateStoredList(
        string op,
        string path,
        string? value,
        string[] expectedStored,
        TitleLanguage[] expectedOrder
    )
    {
        var settings = new ServerSettings();

        Assert.Equal(new[] { "none", "x-main", "en" }, settings.Metadata.SourceDefaults.Images.InternalImageLanguageOrder);

        var patch = new JsonPatchDocument<ServerSettings>();
        patch.Operations.Add(new Operation<ServerSettings>(op, path, null, value));
        patch.ApplyTo(settings);

        Assert.Equal(expectedStored, settings.Metadata.SourceDefaults.Images.InternalImageLanguageOrder);
        Assert.Equal(expectedOrder, settings.Metadata.SourceDefaults.Images.ImageLanguageOrder);
    }

    [Fact]
    public void WholeArrayReplace_OnMetadataSourceImageLanguageOrder_NormalizesList()
    {
        var settings = new ServerSettings();

        var patch = new JsonPatchDocument<ServerSettings>();
        patch.Replace(s => s.Metadata.SourceDefaults.Images.InternalImageLanguageOrder, new List<string> { "en", "de", "en", "nonsense" });
        patch.ApplyTo(settings);

        Assert.Equal(new[] { "en", "de" }, settings.Metadata.SourceDefaults.Images.InternalImageLanguageOrder);
        Assert.Equal(new[] { TitleLanguage.English, TitleLanguage.German }, settings.Metadata.SourceDefaults.Images.ImageLanguageOrder);
    }

    [Fact]
    public void PerItemOps_OnImportExclude_ReflectOnceCacheInvalidated()
    {
        var settings = new ServerSettings();

        // Populate and record the compiled regex cache.
        var oldCount = settings.Import.ExcludeExpressions.Count;
        Assert.Equal(3, oldCount);

        var patch = new JsonPatchDocument<ServerSettings>();
        patch.Operations.Add(new Operation<ServerSettings>("add", "/Import/Exclude/-", null, @"[\\\/]test[\\\/]"));
        patch.ApplyTo(settings);

        Assert.Contains(@"[\\\/]test[\\\/]", settings.Import.Exclude);

        // Mirror SettingsProvider.OnSettingsSaved, which invalidates the cache on every save.
        settings.Import.ResetExcludeRegexes();

        Assert.Equal(oldCount + 1, settings.Import.ExcludeExpressions.Count);
        Assert.Contains(settings.Import.ExcludeExpressions, regex => regex.IsMatch(@"/data/test/file.mkv"));

        // Removing an item must also be reflected once the cache is invalidated.
        patch = new JsonPatchDocument<ServerSettings>();
        patch.Operations.Add(new Operation<ServerSettings>("remove", "/Import/Exclude/0", null, null));
        patch.ApplyTo(settings);
        settings.Import.ResetExcludeRegexes();

        Assert.Equal(oldCount, settings.Import.Exclude.Count);
        Assert.Equal(oldCount, settings.Import.ExcludeExpressions.Count);
    }

    [Theory]
    [InlineData("/AniDb/Username", "some-user", "hunter2")]
    [InlineData("/AniDb/Password", ConfigurationSecrets.Sentinel, "hunter2")]
    [InlineData("/AniDb/Password", "hunter3", "hunter3")]
    [InlineData("/AniDb/Password", "", "")]
    public void PatchingSettings_NeverDestroysASecretItDidNotChange(string path, string value, string expectedPassword)
    {
        // `SettingsController.SetSettings` patches a copy of the current settings
        // and saves it, and the save runs the secret restore pass. A patch that
        // does not mention the password, or that round-trips the sentinel it was
        // handed, has to leave the stored credential standing; only a real new
        // value or an explicit clear may change it.
        var stored = new ServerSettings();
        stored.AniDb.Password = "hunter2";

        var patched = new ServerSettings();
        patched.AniDb.Password = ConfigurationSecrets.Sentinel;

        var patch = new JsonPatchDocument<ServerSettings>();
        patch.Operations.Add(new Operation<ServerSettings>("replace", path, null, value));
        patch.ApplyTo(patched);

        var (restored, errors) = ConfigurationSecrets.Restore(
            JObject.FromObject(patched),
            JObject.FromObject(stored),
            typeof(ServerSettings),
            [.. Enumerable.Range(1, 32).Select(i => (byte)i)]);

        Assert.Empty(errors);
        Assert.Equal(expectedPassword, restored["AniDb"]!["Password"]!.Value<string>() ?? string.Empty);
    }
}
