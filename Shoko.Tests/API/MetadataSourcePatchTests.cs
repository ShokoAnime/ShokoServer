using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.Resolvers;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Unit tests for how APIv3 JSON Patch documents read metadata sources: the
/// server settings take back any source they hand out, and a dictionary key
/// in a path is refused unless it names a registered source.
/// </summary>
public class MetadataSourcePatchTests
{
    private static JsonPatchDocument<T> Patch<T>(params Operation<T>[] operations) where T : class
        => new([.. operations], new ApiContractResolver());

    // A text handed out as a source of its own could no longer become an alias, so a free one proves nothing was handed out.
    private static void AssertNotHandedOut(string text)
        => MetadataSource.Register(text + "-owner", text + "-owner", [text]);

    #region Settings

    // What GET /api/v3/Settings sends for the settings.
    private static JObject GetSettings(ServerSettings settings)
        => JObject.FromObject(settings, JsonSerializer.Create(new JsonSerializerSettings { ContractResolver = new ApiContractResolver() }));

    [Fact]
    public void Settings_TakeBackASourceWhosePluginIsNotLoaded()
    {
        var dormant = MetadataSource.Parse("mspt-dormant");
        var settings = new ServerSettings();
        settings.Language.SeriesTitleSourceOrder = [MetadataSource.AniDB, MetadataSource.TMDB, dormant];
        var sent = (JArray)GetSettings(settings)["Language"]!["SeriesTitleSourceOrder"]!;
        var reordered = new JArray(sent.Reverse());
        var modelState = new ModelStateDictionary();

        SettingsController.ApplyPatch(Patch(new Operation<ServerSettings>("replace", "/Language/SeriesTitleSourceOrder", null, reordered)), settings, modelState);

        Assert.True(modelState.IsValid);
        Assert.Equal([dormant, MetadataSource.TMDB, MetadataSource.AniDB], settings.Language.SeriesTitleSourceOrder);
    }

    [Fact]
    public void Settings_TakeBackAnOldSourceByItsValueAtAnIndex()
    {
        var settings = new ServerSettings();
        settings.Language.DescriptionSourceOrder = [MetadataSource.TMDB, MetadataSource.AniDB];
        var modelState = new ModelStateDictionary();

        SettingsController.ApplyPatch(Patch(new Operation<ServerSettings>("add", "/Language/DescriptionSourceOrder/0", null, "fanart-tv")), settings, modelState);

        Assert.True(modelState.IsValid);
        Assert.Equal(["fanart-tv", "tmdb", "anidb"], settings.Language.DescriptionSourceOrder.Select(source => source.Value));
    }

    [Fact]
    public void OtherPatches_StillRefuseASourceWhosePluginIsNotLoaded()
    {
        var settings = new ServerSettings();
        var modelState = new ModelStateDictionary();

        Patch(new Operation<ServerSettings>("add", "/Language/DescriptionSourceOrder/0", null, "mspt-strict")).ApplyTo(settings, modelState);

        Assert.False(modelState.IsValid);
        AssertNotHandedOut("mspt-strict");
    }

    #endregion

    #region Dictionary Keys

    [Theory]
    [InlineData("/Sources/TMDB")]
    [InlineData("/sources/themoviedb")]
    [InlineData("/Sources/tmdb")]
    public void Keys_TakeARegisteredSource(string path)
    {
        var document = Patch(new Operation<Series.AutoMatchSettings>("add", path, null, false));
        var modelState = new ModelStateDictionary();
        var autoMatchSettings = new Series.AutoMatchSettings();

        Assert.True(MetadataSourcePatchKeys.Validate(document, modelState));
        document.ApplyTo(autoMatchSettings, modelState);

        Assert.True(modelState.IsValid);
        Assert.False(autoMatchSettings.Sources[MetadataSource.TMDB]);
    }

    [Theory]
    [InlineData("add", "/Sources/mspt-path", null)]
    [InlineData("move", "/Sources/tmdb", "/Sources/mspt-path")]
    [InlineData("remove", "/Sources/Locally Generated", null)]
    public void Keys_RefuseTextThatNamesNoRegisteredSource(string op, string path, string? from)
    {
        var modelState = new ModelStateDictionary();

        Assert.False(MetadataSourcePatchKeys.Validate(Patch(new Operation<Series.AutoMatchSettings>(op, path, from, false)), modelState));

        Assert.Single(Assert.Single(modelState.Values).Errors);
    }

    [Fact]
    public void Keys_NeverHandOutAnUnregisteredSource()
    {
        MetadataSourcePatchKeys.Validate(Patch(new Operation<Series.AutoMatchSettings>("add", "/Sources/mspt-never", null, false)), new ModelStateDictionary());

        AssertNotHandedOut("mspt-never");
    }

    [Fact]
    public void Keys_IgnorePathsWithoutASourceKey()
    {
        var modelState = new ModelStateDictionary();

        Assert.True(MetadataSourcePatchKeys.Validate(Patch(new Operation<Series.AutoMatchSettings>("replace", "/Sources", null, new Dictionary<string, bool>())), modelState));
        Assert.True(MetadataSourcePatchKeys.Validate(Patch(new Operation<Series.AutoMatchSettings>("replace", "/Unknown/mspt-other", null, false)), modelState));
        Assert.True(modelState.IsValid);
    }

    #endregion
}
