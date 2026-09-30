using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Shoko.Abstractions.Config.Events;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Services;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;
using Shoko.Tests.Actors;
using Xunit;

namespace Shoko.Tests.Settings;

/// <summary>
///   A configuration save names the JSON paths it changed, never their values, and the actor
///   who saved it.
/// </summary>
public sealed class ConfigurationChangedPathsTests : IDisposable
{
    private readonly string _dataPath = Path.Join(Path.GetTempPath(), $"shoko-changed-paths-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
            Directory.Delete(_dataPath, recursive: true);
    }

    #region Diff

    [Fact]
    public void Diff_NamesChangedAddedAndRemovedLeaves()
    {
        var paths = JsonPathDiff.GetChangedPaths(
            """{"$schema":"a","Web":{"Port":8111,"Host":"x"},"Old":1,"Same":{"A":[1,2]}}""",
            """{"$schema":"b","Web":{"Port":8112,"Host":"x"},"New":{"B":true},"Same":{"A":[1,2]}}"""
        );

        Assert.Equal(["Web.Port", "New.B", "Old"], paths);
    }

    [Fact]
    public void Diff_ArraysByIndexOrWhole()
    {
        Assert.Equal(["List[1]"], JsonPathDiff.GetChangedPaths("""{"List":["a","b"]}""", """{"List":["a","c"]}"""));
        Assert.Equal(["List"], JsonPathDiff.GetChangedPaths("""{"List":["a","b"]}""", """{"List":["a"]}"""));
    }

    [Fact]
    public void Diff_KeysWithDotsAreQuoted()
    {
        Assert.Equal(["Plugins.EnabledPlugins['Fumi.Plugin']"], JsonPathDiff.GetChangedPaths(
            """{"Plugins":{"EnabledPlugins":{}}}""",
            """{"Plugins":{"EnabledPlugins":{"Fumi.Plugin":true}}}"""
        ));
    }

    [Fact]
    public void Diff_NothingBefore_NamesEveryLeaf()
    {
        Assert.Equal(["A", "B.C"], JsonPathDiff.GetChangedPaths(null, """{"$schema":"x","A":1,"B":{"C":2}}"""));
    }

    #endregion

    #region Service

    [Fact]
    public async Task Save_RaisesTheChangedPathsAndActorWithoutValues()
    {
        const string NewPassword = "a-very-secret-password-x9y8";
        var applicationPaths = new Mock<IApplicationPaths>(MockBehavior.Loose);
        applicationPaths.SetupGet(paths => paths.DataPath).Returns(_dataPath);
        applicationPaths.SetupGet(paths => paths.ConfigurationsPath).Returns(Path.Join(_dataPath, "configurations"));
        var service = new ConfigurationService(NullLoggerFactory.Instance, applicationPaths.Object, new Mock<IPluginManager>(MockBehavior.Loose).Object);
        service.Save(service.Load<ServerSettings>());

        // The save above raises its own event on the thread pool, which may still
        // arrive after this handler is attached, so only this save's event counts.
        var token = ActorContextTests.Token("settings-page");
        var saved = new TaskCompletionSource<ConfigurationSavedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Saved += (_, e) =>
        {
            if (ReferenceEquals(e.Actor, token))
                saved.TrySetResult(e);
        };
        var settings = service.Load<ServerSettings>();
        settings.AniDb.Password = NewPassword;
        settings.Web.Port = 18111;
        using (ActorContext.Begin(token))
            Assert.True(service.Save(settings));

        var e = await saved.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(["AniDb.Password", "Web.Port"], [.. e.ChangedPaths]);
        Assert.Same(token, e.Actor);
        var serialized = JsonConvert.SerializeObject(new { e.ChangedPaths, e.Actor?.Device });
        Assert.DoesNotContain(NewPassword, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("18111", serialized, StringComparison.Ordinal);
    }

    #endregion
}
