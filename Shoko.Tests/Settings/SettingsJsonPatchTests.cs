using System.Collections.Generic;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Settings;

public class SettingsJsonPatchTests
{
    [Fact]
    public void PerItemReplace_OnTmdbImageLanguageOrder_MutatesStoredList()
    {
        var settings = new ServerSettings();

        Assert.Equal(new[] { "none", "x-main", "en" }, settings.TMDB.InternalImageLanguageOrder);

        var patch = new JsonPatchDocument<ServerSettings>();
        patch.Operations.Add(new Operation<ServerSettings>("replace", "/TMDB/ImageLanguageOrder/0", null, "fr"));
        patch.ApplyTo(settings);

        Assert.Equal(new[] { "fr", "x-main", "en" }, settings.TMDB.InternalImageLanguageOrder);
        Assert.Equal(
            new[] { TitleLanguage.French, TitleLanguage.Main, TitleLanguage.English },
            settings.TMDB.ImageLanguageOrder);
    }

    [Fact]
    public void PerItemRemove_OnTmdbImageLanguageOrder_MutatesStoredList()
    {
        var settings = new ServerSettings();

        var patch = new JsonPatchDocument<ServerSettings>();
        patch.Operations.Add(new Operation<ServerSettings>("remove", "/TMDB/ImageLanguageOrder/0", null, null));
        patch.ApplyTo(settings);

        Assert.Equal(new[] { "x-main", "en" }, settings.TMDB.InternalImageLanguageOrder);
        Assert.Equal(new[] { TitleLanguage.Main, TitleLanguage.English }, settings.TMDB.ImageLanguageOrder);
    }

    [Fact]
    public void PerItemAdd_OnTmdbImageLanguageOrder_MutatesStoredList()
    {
        var settings = new ServerSettings();

        var patch = new JsonPatchDocument<ServerSettings>();
        patch.Operations.Add(new Operation<ServerSettings>("add", "/TMDB/ImageLanguageOrder/-", null, "de"));
        patch.ApplyTo(settings);

        Assert.Equal(new[] { "none", "x-main", "en", "de" }, settings.TMDB.InternalImageLanguageOrder);
        Assert.Equal(
            new[] { TitleLanguage.None, TitleLanguage.Main, TitleLanguage.English, TitleLanguage.German },
            settings.TMDB.ImageLanguageOrder);
    }

    [Fact]
    public void WholeArrayReplace_OnTmdbImageLanguageOrder_NormalizesList()
    {
        var settings = new ServerSettings();

        var patch = new JsonPatchDocument<ServerSettings>();
        patch.Replace(s => s.TMDB.InternalImageLanguageOrder, new List<string> { "en", "de", "en", "nonsense" });
        patch.ApplyTo(settings);

        Assert.Equal(new[] { "en", "de" }, settings.TMDB.InternalImageLanguageOrder);
        Assert.Equal(new[] { TitleLanguage.English, TitleLanguage.German }, settings.TMDB.ImageLanguageOrder);
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

    [Fact]
    public void InvalidOperation_SurfacesInModelState()
    {
        var settings = new ServerSettings();
        var modelState = new ModelStateDictionary();

        var patch = new JsonPatchDocument<ServerSettings>();
        patch.Operations.Add(new Operation<ServerSettings>("replace", "/TMDB/ImageLanguageOrder/99", null, "fr"));
        patch.ApplyTo(settings, modelState);

        Assert.False(modelState.IsValid);
    }
}
