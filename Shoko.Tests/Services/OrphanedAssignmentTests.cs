using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers how <see cref="MetadataProviderManager"/> hands an assignment whose provider is gone to
/// the first provider now claiming its source, which is what keeps a source working when it moves from
/// the core into a plugin and its provider's ID changes.
/// </summary>
public class OrphanedAssignmentTests
{
    private static readonly Guid s_gone = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid s_plugin = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Guid s_other = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static MetadataProviderManager.ProviderClaim Claim(Guid id, bool links = true, params MetadataSource[] sources)
        => new(id, id.ToString()[..4], new HashSet<MetadataSource>(sources), new HashSet<MetadataEntityType> { MetadataEntityType.Series, MetadataEntityType.Episode }, links);

    private static MetadataServiceSettings Settings(Guid? series, Guid? autoLinker)
        => new()
        {
            Sources =
            {
                [TestSources.AniList] = new()
                {
                    Enabled = { [MetadataEntityType.Series] = series, [MetadataEntityType.Episode] = series },
                    AutoLinker = autoLinker,
                    AutoLink = true,
                },
            },
        };

    [Fact]
    public void AnAssignmentWhoseProviderIsGoneGoesToTheClaimant()
    {
        var settings = Settings(s_gone, s_gone);

        var adoptions = MetadataProviderManager.AdoptOrphanedAssignments(settings, [Claim(s_plugin, true, TestSources.AniList)]);

        var decisions = settings.Sources[TestSources.AniList];
        Assert.Equal(s_plugin, decisions.Enabled[MetadataEntityType.Series]);
        Assert.Equal(s_plugin, decisions.Enabled[MetadataEntityType.Episode]);
        Assert.Equal(s_plugin, decisions.AutoLinker);
        // The admin's choice to auto-link travels with the assignment.
        Assert.True(decisions.AutoLink);
        Assert.Equal(3, adoptions.Count);
    }

    [Fact]
    public void AnAssignmentLeftEmptyOnPurposeStaysEmpty()
    {
        var settings = Settings(null, null);

        var adoptions = MetadataProviderManager.AdoptOrphanedAssignments(settings, [Claim(s_plugin, true, TestSources.AniList)]);

        Assert.Null(settings.Sources[TestSources.AniList].Enabled[MetadataEntityType.Series]);
        Assert.Null(settings.Sources[TestSources.AniList].AutoLinker);
        Assert.Empty(adoptions);
    }

    [Fact]
    public void OfTwoClaimantsTheFirstRegisteredTakesOver()
    {
        var settings = Settings(s_gone, s_gone);

        MetadataProviderManager.AdoptOrphanedAssignments(settings, [Claim(s_other, true, TestSources.AniList), Claim(s_plugin, true, TestSources.AniList)]);

        Assert.Equal(s_other, settings.Sources[TestSources.AniList].Enabled[MetadataEntityType.Series]);
        Assert.Equal(s_other, settings.Sources[TestSources.AniList].AutoLinker);
    }

    [Fact]
    public void AutoLinkingGoesToTheFirstClaimantAbleToLink()
    {
        var settings = Settings(s_gone, s_gone);

        MetadataProviderManager.AdoptOrphanedAssignments(settings, [Claim(s_other, false, TestSources.AniList), Claim(s_plugin, true, TestSources.AniList)]);

        Assert.Equal(s_other, settings.Sources[TestSources.AniList].Enabled[MetadataEntityType.Series]);
        Assert.Equal(s_plugin, settings.Sources[TestSources.AniList].AutoLinker);
    }

    [Fact]
    public void AnAssignmentWhoseProviderIsStillThereIsLeftAlone()
    {
        var settings = Settings(s_other, s_other);

        MetadataProviderManager.AdoptOrphanedAssignments(settings, [Claim(s_other, true, TestSources.AniList), Claim(s_plugin, true, TestSources.AniList)]);

        Assert.Equal(s_other, settings.Sources[TestSources.AniList].Enabled[MetadataEntityType.Series]);
    }

    [Fact]
    public void AutoLinkingOnlyGoesToAProviderThatCanLink()
    {
        var settings = Settings(s_gone, s_gone);

        MetadataProviderManager.AdoptOrphanedAssignments(settings, [Claim(s_plugin, false, TestSources.AniList)]);

        Assert.Equal(s_plugin, settings.Sources[TestSources.AniList].Enabled[MetadataEntityType.Series]);
        Assert.Equal(s_gone, settings.Sources[TestSources.AniList].AutoLinker);
    }
}
