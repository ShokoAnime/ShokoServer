using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Config.Exceptions;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Exceptions;
using Shoko.Abstractions.Core.Services;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Checks on a started server that a core API the web UI in use needs stays
/// on: turning it off is refused through the system service and through a
/// settings save, while an API no web UI needs can be turned off and on.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class CoreApiTests(DatabaseMigrationFixture fixture)
{
    [Fact]
    public void TheShokoWebUIKeepsAPIv3On()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var system = fixture.Services.GetRequiredService<ISystemService>();
        var webUIs = fixture.Services.GetRequiredService<WebUIService>();
        var settingsProvider = fixture.Services.GetRequiredService<ISettingsProvider>();
        Assert.SkipUnless(webUIs.IsEnabled, "No web UI is served.");

        Assert.Equal(CoreApis.V3, webUIs.ActiveProvider.RequiredApis);
        Assert.True(system.RunningApis.HasFlag(CoreApis.V3));

        Assert.True(system.ApisNeededByWebUIs.HasFlag(CoreApis.V3));
        var refusal = Assert.Throws<CoreApiChangeRefusedException>(() => system.SetApiEnabled(CoreApis.V3, false));
        Assert.Equal(CoreApis.V3, refusal.Apis);
        Assert.Equal(CoreApiChangeRefusal.NeededByWebUI, refusal.Reason);
        Assert.Equal(webUIs.ActiveProvider.ID, Assert.Single(refusal.WebUIs).ID);
        Assert.Contains(webUIs.ActiveProvider.Name, refusal.Message);

        var settings = settingsProvider.GetSettings(copy: true);
        settings.Web.EnableAPIv3 = false;
        Assert.Throws<ConfigurationValidationException>(() => settingsProvider.SaveSettings(settings));
        Assert.True(system.ApisOnNextStart.HasFlag(CoreApis.V3));
    }

    [Fact]
    public void AnApiNoWebUINeedsTurnsOffAndOn()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var system = fixture.Services.GetRequiredService<ISystemService>();
        Assert.SkipWhen(Environment.GetEnvironmentVariable("SHOKO_API_V2_ENABLED") is not null, "An environment variable sets APIv2.");
        var wasOn = system.ApisOnNextStart.HasFlag(CoreApis.V2);
        try
        {
            system.SetApiEnabled(CoreApis.V2, false);
            Assert.False(system.ApisOnNextStart.HasFlag(CoreApis.V2));

            system.SetApiEnabled(CoreApis.V2, true);
            Assert.True(system.ApisOnNextStart.HasFlag(CoreApis.V2));
        }
        finally
        {
            system.SetApiEnabled(CoreApis.V2, wasOn);
        }

        Assert.Throws<ArgumentException>(() => system.SetApiEnabled(CoreApis.None, true));
    }
}
