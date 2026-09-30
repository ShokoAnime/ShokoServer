using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Shoko.Server.API.v3.Controllers;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// The writes that change what every user sees are an admin's only: orderings and hidden
/// episodes, an entity's images, the texts of any entry and the scheduled actions. The
/// scheduled actions are an admin's to read too, while any user may read the texts.
/// </summary>
public class AdminAuthorizationTests
{
    private const BindingFlags Actions = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static bool IsWrite(MethodInfo action)
        => action.GetCustomAttributes<HttpMethodAttribute>().Any(attribute => attribute.HttpMethods.Any(method => method != "GET"));

    private static bool IsRead(MethodInfo action)
        => action.GetCustomAttributes<HttpMethodAttribute>().Any(attribute => attribute.HttpMethods.Contains("GET"));

    private static bool RequiresAdmin(Type controller, MethodInfo action)
        => action.GetCustomAttributes<AuthorizeAttribute>().Concat(controller.GetCustomAttributes<AuthorizeAttribute>())
            .Any(attribute => attribute.Policy == "admin");

    [Theory]
    [InlineData(typeof(SeriesOrderingController), null)]
    [InlineData(typeof(TextManagementController), null)]
    [InlineData(typeof(ScheduledActionController), null)]
    [InlineData(typeof(MetadataController), nameof(MetadataController.SetEpisodeHiddenState))]
    [InlineData(typeof(EpisodeController), nameof(EpisodeController.PostEpisodeSetHidden))]
    [InlineData(typeof(TmdbController), nameof(TmdbController.SetPreferredTmdbShowOrdering))]
    [InlineData(typeof(TmdbController), nameof(TmdbController.SetHiddenStateForTmdbEpisodeByEpisodeID))]
    [InlineData(typeof(SeriesController), nameof(SeriesController.EnableSeriesImageForType))]
    [InlineData(typeof(EpisodeController), nameof(EpisodeController.EnableEpisodeImageForType))]
    [InlineData(typeof(GroupController), nameof(GroupController.EnableGroupImageForType))]
    [InlineData(typeof(SeriesController), nameof(SeriesController.UploadImageForSeries))]
    [InlineData(typeof(EpisodeController), nameof(EpisodeController.UploadImageForEpisode))]
    [InlineData(typeof(GroupController), nameof(GroupController.UploadImageForGroup))]
    public void WritesRequireAnAdmin(Type controller, string? action)
    {
        List<MethodInfo> writes = action is null
            ? [.. controller.GetMethods(Actions).Where(IsWrite)]
            : [controller.GetMethod(action, Actions) ?? throw new MissingMethodException(controller.Name, action)];
        Assert.NotEmpty(writes);

        var controllerPolicies = controller.GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy);
        Assert.All(writes, write =>
        {
            Assert.False(write.IsDefined(typeof(AllowAnonymousAttribute)), write.Name);
            Assert.Contains("admin", write.GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy).Concat(controllerPolicies));
        });
    }

    [Theory]
    [InlineData(typeof(ScheduledActionController), true)]
    [InlineData(typeof(TextManagementController), false)]
    public void Reads(Type controller, bool adminOnly)
    {
        List<MethodInfo> reads = [.. controller.GetMethods(Actions).Where(IsRead)];
        Assert.NotEmpty(reads);
        Assert.All(reads, read => Assert.Equal(adminOnly, RequiresAdmin(controller, read)));
    }
}
