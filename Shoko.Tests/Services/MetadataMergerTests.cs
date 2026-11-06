using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   The merged genres rank by how many sources list them and leave out the options'
///   excluded genres, and the saved options are kept in the settings.
/// </summary>
public class MetadataMergerTests
{
    private static ITag Genre(MetadataSource source, string name)
        => Mock.Of<ITag>(tag => tag.ID == new MetadataGuid(source, MetadataEntityType.Tag, name)
            && tag.Source == source
            && tag.Name == name
            && tag.Kind == TagKind.Genre);

    private static ITag AnidbGenre(string name)
        => Mock.Of<ITag>(tag => tag.ID == new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Tag, name)
            && tag.Source == MetadataSource.AniDB
            && tag.Name == name);

    [Fact]
    public void GenresMostSourcesAgreeOnComeFirstAndTheExcludedOnesAreLeftOut()
    {
        var linked = new List<MetadataMerger.LinkedEntry>
        {
            new(MetadataSource.TMDB, [], [Genre(MetadataSource.TMDB, "Action"), Genre(MetadataSource.TMDB, "Drama")]),
        };

        var merged = MetadataMerger.MergeGenres(
            [AnidbGenre("action"), AnidbGenre("comedy")],
            linked,
            [MetadataSource.AniDB, MetadataSource.TMDB],
            5,
            ["Comedy"]
        );

        Assert.Equal(["action", "Drama"], merged.Select(genre => genre.Name));
    }

    [Fact]
    public void TheOptionsAreSavedAndALimitOutOfRangeIsRefused()
    {
        var settings = new MetadataServiceSettings();
        var configurationService = new Mock<IConfigurationService>();
        configurationService.Setup(service => service.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>())).Returns(() => settings);
        configurationService.Setup(service => service.Save(It.IsAny<ConfigurationInfo>(), It.IsAny<IConfiguration>()))
            .Callback((ConfigurationInfo _, IConfiguration saved) => settings = (MetadataServiceSettings)saved);
        var service = new MetadataMerger(null!, Mock.Of<IMetadataService>(), new ConfigurationProvider<MetadataServiceSettings>(configurationService.Object));

        service.Options = new() { SourceOrder = [MetadataSource.TMDB, MetadataSource.AniDB], GenreLimit = 3, ExcludedGenres = ["anime"] };

        Assert.Equal([MetadataSource.TMDB, MetadataSource.AniDB], service.Options.SourceOrder);
        Assert.Equal(3, service.Options.GenreLimit);
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Options = new() { GenreLimit = -1 });
    }
}
