using System.Collections.Generic;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Server.API.Resolvers;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Settings;

/// <summary>
/// A settings patch may still carry <c>WebUI_Settings</c>, which the server
/// settings no longer hold; the settings route takes those operations out and
/// applies them to the caller's own web UI settings.
/// </summary>
public class WebUISettingsPatchTests
{
    private static JsonPatchDocument<ServerSettings> Patch(params Operation<ServerSettings>[] operations)
        => new(new List<Operation<ServerSettings>>(operations), new ApiContractResolver());

    [Fact]
    public void TheWebUISettingsAreTakenOutOfThePatch()
    {
        var patch = Patch(new("replace", "/WebUI_Settings", null, """{"theme":"light"}"""), new("replace", "/FirstRun", null, false));

        Assert.True(SettingsController.TryTakeWebUISettings(patch, new ModelStateDictionary(), out var changed, out var settings));

        Assert.True(changed);
        Assert.Equal("light", settings?["theme"]?.ToString());
        var remaining = Assert.Single(patch.Operations);
        Assert.Equal("/FirstRun", remaining.path);
    }

    [Theory]
    [InlineData("remove", null)]
    [InlineData("replace", "")]
    public void RemovingOrBlankingThemDeletesThem(string op, string? value)
    {
        var patch = Patch(new Operation<ServerSettings>(op, "/WebUI_Settings", null, value!));

        Assert.True(SettingsController.TryTakeWebUISettings(patch, new ModelStateDictionary(), out var changed, out var settings));

        Assert.True(changed);
        Assert.Null(settings);
    }

    [Fact]
    public void TextThatIsNotAJsonObjectIsRefused()
    {
        var modelState = new ModelStateDictionary();

        Assert.False(SettingsController.TryTakeWebUISettings(Patch(new Operation<ServerSettings>("replace", "/WebUI_Settings", null, "not json")), modelState, out _, out _));
        Assert.False(modelState.IsValid);
    }

    [Fact]
    public void APatchWithoutThemLeavesThemAlone()
    {
        Assert.True(SettingsController.TryTakeWebUISettings(Patch(new Operation<ServerSettings>("replace", "/FirstRun", null, false)), new ModelStateDictionary(), out var changed, out _));

        Assert.False(changed);
    }
}
