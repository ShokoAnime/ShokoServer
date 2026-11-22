using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core;
using Shoko.Server.API;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.API;

public class ServedApisTests
{
    [Theory]
    [InlineData(true, true, CoreApis.V2 | CoreApis.V3)]
    [InlineData(false, true, CoreApis.V3)]
    [InlineData(false, false, CoreApis.None)]
    public void FromSettings_ReadsBothFlags(bool v2, bool v3, CoreApis expected)
        => Assert.Equal(expected, ServedApis.FromSettings(new WebSettings { EnableAPIv2 = v2, EnableAPIv3 = v3 }));

    [Theory]
    [InlineData(CoreApis.V3, "APIv3")]
    [InlineData(CoreApis.V2 | CoreApis.V3, "APIv2 and APIv3")]
    public void Describe_NamesTheApis(CoreApis apis, string expected)
        => Assert.Equal(expected, ServedApis.Describe(apis));

    [Fact]
    public void Settle_KeepsTheFirstAnswer()
    {
        var served = new ServedApis();

        Assert.True(served.Settle(CoreApis.V3));
        Assert.False(served.Settle(CoreApis.V2));
        Assert.True(served.Serves(CoreApis.V3));
        Assert.False(served.Serves(CoreApis.V2));
    }

    [Fact]
    public void Change_AppliesOnlyOnceSettled_AndTellsTheListeners()
    {
        var served = new ServedApis();
        var seen = new List<CoreApis>();
        served.Changed += (_, before) => seen.Add(before);

        Assert.False(served.Change(CoreApis.V3));
        served.Settle(CoreApis.V2 | CoreApis.V3);
        Assert.False(served.Change(CoreApis.V2 | CoreApis.V3));
        Assert.True(served.Change(CoreApis.V3));

        Assert.False(served.Serves(CoreApis.V2));
        Assert.Equal([CoreApis.V2 | CoreApis.V3], seen);
    }

    [Fact]
    public void Change_RunsEveryListener_WhenOneThrows()
    {
        var served = new ServedApis();
        served.Settle(CoreApis.V3);
        var reached = false;
        served.Changed += (_, _) => throw new InvalidOperationException("first");
        served.Changed += (_, _) => reached = true;

        Assert.Throws<InvalidOperationException>(() => served.Change(CoreApis.V2));
        Assert.True(reached);
        Assert.True(served.Serves(CoreApis.V2));
    }

    [Fact]
    public void ChangeProvider_SignalsAChange()
    {
        var served = new ServedApis();
        served.Settle(CoreApis.V3);
        using var provider = new ServedApisChangeProvider(served, new ServiceCollection().BuildServiceProvider());
        var token = provider.GetChangeToken();

        served.Change(CoreApis.V2 | CoreApis.V3);

        Assert.True(token.HasChanged);
        Assert.False(provider.GetChangeToken().HasChanged);
    }

    [Fact]
    public void BeforeTheyAreSettled_EveryCoreControllerIsFound_AndNothingIsSettled()
    {
        var served = new ServedApis();

        var controllers = FindControllers(served);

        Assert.Contains(typeof(Shoko.Server.API.v2.Modules.Common).GetTypeInfo(), controllers);
        Assert.Contains(typeof(Shoko.Server.API.v3.Controllers.ActionController).GetTypeInfo(), controllers);
        Assert.False(served.IsSettled);
    }

    [Fact]
    public void OnceTheyAreSettled_OnlyTheServedApisControllersAreFound()
    {
        var served = new ServedApis();
        served.Settle(CoreApis.V3);

        var controllers = FindControllers(served);

        Assert.DoesNotContain(typeof(Shoko.Server.API.v2.Modules.Common).GetTypeInfo(), controllers);
        Assert.Contains(typeof(Shoko.Server.API.v3.Controllers.ActionController).GetTypeInfo(), controllers);
    }

    private static IList<TypeInfo> FindControllers(ServedApis served)
    {
        var manager = new ApplicationPartManager();
        manager.ApplicationParts.Add(new AssemblyPart(typeof(ServedApis).Assembly));
        manager.FeatureProviders.Add(new ApiVersionControllerFeatureProvider(served));
        var feature = new ControllerFeature();
        manager.PopulateFeature(feature);
        return feature.Controllers;
    }
}
