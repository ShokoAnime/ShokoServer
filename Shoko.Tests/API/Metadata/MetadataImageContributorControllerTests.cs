using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.API.Metadata;

/// <summary>
/// Covers the image contributor routes: listing them and setting the sources
/// and kinds each one is on for.
/// </summary>
public class MetadataImageContributorControllerTests
{
    #region Fixture

    private sealed class Contributor : IMetadataImageContributor
    {
        public string Name => "Art";

        public MetadataSource Source => TestSources.Plugin;

        public MetadataEntityScope Scope => MetadataEntityScope.ForSource(MetadataSource.TMDB, MetadataEntityType.Series, MetadataEntityType.Movie);

        public Task<IReadOnlyList<ImageCandidate>?> GetImages(IMetadata entity, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ImageCandidate>?>(null);
    }

    private static MetadataImageContributorInfo Info()
    {
        var contributor = new Contributor();
        return new()
        {
            ID = Guid.NewGuid(),
            Version = new(1, 0),
            Name = contributor.Name,
            Description = string.Empty,
            Contributor = contributor,
            PluginInfo = PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), Guid.NewGuid()),
            Source = contributor.Source,
            MaxConcurrentJobs = 2,
            AvailableScope = contributor.Scope,
            EnabledScope = contributor.Scope,
        };
    }

    private static MetadataImageContributorController Controller(Mock<IMetadataImageContributorManager> manager)
        => new(new StubSettingsProvider(new ServerSettings()), manager.Object)
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider() },
            },
        };

    private static Mock<IMetadataImageContributorManager> Manager(MetadataImageContributorInfo info)
    {
        var manager = new Mock<IMetadataImageContributorManager>();
        manager.SetupGet(m => m.ImageContributors).Returns([info]);
        manager.Setup(m => m.GetImageContributorInfo(info.ID)).Returns(info);
        return manager;
    }

    private static int StatusOf<T>(ActionResult<T> result)
        => result.Result switch
        {
            IStatusCodeActionResult { StatusCode: { } code } => code,
            _ => 200,
        };

    #endregion

    [Fact]
    public void ContributorsAreListedWithWhatTheyCanDoAndAreOnFor()
    {
        var info = Info();
        var controller = Controller(Manager(info));

        var model = Assert.Single(controller.GetImageContributors().Value!);

        var tmdb = Assert.Single(model.Enabled);
        Assert.Equal(MetadataSource.TMDB, tmdb.Source);
        Assert.Equal([MetadataEntityType.Series, MetadataEntityType.Movie], tmdb.EntityTypes);
        Assert.Empty(controller.GetImageContributors(Guid.NewGuid()).Value!);
        Assert.IsType<NotFoundObjectResult>(controller.GetImageContributor(Guid.NewGuid()).Result);
    }

    [Fact]
    public void AContributorIsTurnedOnForThePairsGivenAndRefusesPairsItCannotServe()
    {
        var info = Info();
        var manager = Manager(info);
        var controller = Controller(manager);

        var refused = controller.UpdateImageContributor(info.ID, new()
        {
            Enabled = [new() { Source = MetadataSource.AniDB, EntityTypes = [MetadataEntityType.Series] }],
        });
        var updated = controller.UpdateImageContributor(info.ID, new()
        {
            Enabled = [new() { Source = MetadataSource.TMDB, EntityTypes = [MetadataEntityType.Movie] }],
        });

        Assert.Equal(400, StatusOf(refused));
        Assert.Equal(200, StatusOf(updated));
        manager.Verify(m => m.SetImageContributorEnabled(info.ID, MetadataEntityScope.Single(MetadataSource.TMDB, MetadataEntityType.Movie)), Times.Once);
        manager.Verify(m => m.SetImageContributorEnabled(It.IsAny<Guid>(), It.IsAny<MetadataEntityScope>()), Times.Once);
        Assert.IsType<NotFoundObjectResult>(controller.UpdateImageContributor(Guid.NewGuid(), new()).Result);
    }
}
