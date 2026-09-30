using System.Linq;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.API.Resolvers;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Pins the <c>TMDB</c> section of the settings endpoint to the keys it had
/// before the auto-link switches and the image settings moved, both in what
/// it sends and in what a patch of them changes.
/// </summary>
public class LegacyTmdbSettingsTests
{
    private static readonly JsonSerializer _serializer = JsonSerializer.Create(new JsonSerializerSettings { ContractResolver = new ApiContractResolver() });

    private static JObject Serialize(ServerSettings settings, MetadataSourceSettings? decisions)
    {
        var json = JObject.FromObject(settings, _serializer);
        LegacyTmdbSettings.AddTo(json, settings, decisions, _serializer);
        return json;
    }

    private static ServerSettings WithTmdbImages(MetadataSourceImageSettings images)
    {
        var settings = new ServerSettings();
        images.Source = MetadataSource.TMDB;
        settings.Image.MetadataSources.Add(images);
        return settings;
    }

    private static JsonPatchDocument<ServerSettings> Patch(params Operation<ServerSettings>[] operations)
        => new([.. operations], new ApiContractResolver());

    #region Output

    [Fact]
    public void TheSection_HasEveryKeyItHad()
    {
        var json = Serialize(new ServerSettings(), new() { AutoLink = true, AutoLinkRestricted = false });

        var keys = ((JObject)json["TMDB"]!).Properties().Select(property => property.Name);
        string[] expected =
        [
            "AutoLink", "AutoLinkRestricted", "ConsiderExistingOtherLinks", "DownloadAllTitles", "DownloadAllOverviews", "DownloadAllContentRatings",
            "ImageLanguageOrder", "AutoDownloadCrewAndCast", "AutoDownloadCollections", "AutoDownloadAlternateOrdering", "AutoDownloadNetworks",
            "AutoDownloadBackdrops", "MaxAutoBackdrops", "AutoDownloadPosters", "MaxAutoPosters", "AutoDownloadLogos", "MaxAutoLogos",
            "AutoDownloadThumbnails", "MaxAutoThumbnails", "AutoDownloadStaffImages", "MaxAutoStaffImages", "AutoDownloadStudioImages",
            "UserApiKey",
        ];
        Assert.Empty(expected.Except(keys));
        Assert.True(json["TMDB"]!["AutoLink"]!.Value<bool>());
        Assert.False(json["TMDB"]!["AutoLinkRestricted"]!.Value<bool>());
    }

    [Fact]
    public void TheImageKeys_AreTmdbsOwn()
    {
        var settings = WithTmdbImages(new() { MaxAutoPosters = 3, AutoDownloadLogos = false, InternalImageLanguageOrder = ["en", "ja"] });

        var tmdb = Serialize(settings, null)["TMDB"]!;

        Assert.Equal(3, tmdb["MaxAutoPosters"]!.Value<int>());
        Assert.False(tmdb["AutoDownloadLogos"]!.Value<bool>());
        Assert.Equal(["en", "ja"], tmdb["ImageLanguageOrder"]!.Values<string>());
        Assert.False(tmdb["AutoLink"]!.Value<bool>());
    }

    [Fact]
    public void TheImageKeys_FallBackToTheDefaults()
    {
        var settings = new ServerSettings();
        settings.Image.MetadataSourceDefaults.MaxAutoBackdrops = 7;

        Assert.Equal(7, Serialize(settings, null)["TMDB"]!["MaxAutoBackdrops"]!.Value<int>());
    }

    #endregion

    #region Input

    [Fact]
    public void APatchOfAnImageKey_ChangesTmdbsEntry()
    {
        var settings = WithTmdbImages(new());
        var patch = Patch(
            new("replace", "/TMDB/MaxAutoPosters", null, 5),
            new("replace", "/TMDB/ImageLanguageOrder/0", null, "ja"),
            new("replace", "/TMDB/DownloadAllTitles", null, true));
        var modelState = new ModelStateDictionary();

        var switches = LegacyTmdbSettings.Translate(patch, settings, null, modelState);
        SettingsController.ApplyPatch(patch, settings, modelState);

        Assert.True(modelState.IsValid);
        Assert.Empty(switches);
        var tmdb = settings.Image.MetadataSources.Single();
        Assert.Equal(5, tmdb.MaxAutoPosters);
        Assert.Equal("ja", tmdb.InternalImageLanguageOrder[0]);
        Assert.True(settings.TMDB.DownloadAllTitles);
        Assert.Equal(10, settings.Image.MetadataSourceDefaults.MaxAutoPosters);
    }

    [Fact]
    public void APatchOfAnImageKey_MakesTmdbsEntryFromTheDefaultsWhenItHasNone()
    {
        var settings = new ServerSettings();
        settings.Image.MetadataSourceDefaults.MaxAutoLogos = 4;
        var patch = Patch(new Operation<ServerSettings>("replace", "/tmdb/autodownloadposters", null, false));
        var modelState = new ModelStateDictionary();

        LegacyTmdbSettings.Translate(patch, settings, null, modelState);
        SettingsController.ApplyPatch(patch, settings, modelState);

        Assert.True(modelState.IsValid);
        var tmdb = Assert.Single(settings.Image.MetadataSources);
        Assert.Equal(MetadataSource.TMDB, tmdb.Source);
        Assert.False(tmdb.AutoDownloadPosters);
        Assert.Equal(4, tmdb.MaxAutoLogos);
        Assert.True(settings.Image.MetadataSourceDefaults.AutoDownloadPosters);
    }

    [Fact]
    public void APatchOfTheAutoLinkSwitches_IsTakenOutAndReturned()
    {
        var settings = new ServerSettings();
        var patch = Patch(
            new("replace", "/TMDB/AutoLink", null, false),
            new("replace", "/TMDB/AutoLinkRestricted", null, JToken.FromObject(true)),
            new("test", "/TMDB/AutoLink", null, false));
        var modelState = new ModelStateDictionary();

        var switches = LegacyTmdbSettings.Translate(patch, settings, new() { AutoLink = true }, modelState);

        Assert.True(modelState.IsValid);
        Assert.Empty(patch.Operations);
        Assert.False(switches[LegacyTmdbSettings.AutoLink]);
        Assert.True(switches[LegacyTmdbSettings.AutoLinkRestricted]);
    }

    [Theory]
    [InlineData("replace", "yes")]
    [InlineData("remove", null)]
    [InlineData("test", true)]
    public void APatchOfAnAutoLinkSwitch_ThatCannotBeDone_IsRefused(string op, object? value)
    {
        var modelState = new ModelStateDictionary();

        LegacyTmdbSettings.Translate(Patch(new Operation<ServerSettings>(op, "/TMDB/AutoLink", null, value)), new ServerSettings(), new() { AutoLink = false }, modelState);

        Assert.False(modelState.IsValid);
    }

    #endregion
}
