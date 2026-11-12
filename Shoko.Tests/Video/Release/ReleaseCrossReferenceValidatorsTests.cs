using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Video.Release;

/// <summary>
///   How the validators of every plugin are run over a release on save.
/// </summary>
public class ReleaseCrossReferenceValidatorsTests
{
    #region Helpers

    private static readonly MetadataGuid _pluginEpisode = new(TestSources.Plugin, MetadataEntityType.Episode, "e1");

    private static readonly MetadataGuid _pluginSeries = new(TestSources.Plugin, MetadataEntityType.Series, "s1");

    private static readonly MetadataEntityScope _pluginScope = MetadataEntityScope.ForSource(TestSources.Plugin, MetadataEntityType.Episode, MetadataEntityType.Series);

    private sealed class TestValidator(string name, MetadataEntityScope scope, Func<ReleaseCrossReferenceValidationContext, ReleaseCrossReferenceValidationResult?> validate)
        : IReleaseCrossReferenceValidator
    {
        public List<string> Seen { get; } = [];

        public string Name => name;

        public MetadataEntityScope Scope => scope;

        public Task<ReleaseCrossReferenceValidationResult?> Validate(ReleaseCrossReferenceValidationContext context, CancellationToken cancellationToken)
        {
            Seen.Add(string.Join(",", context.CrossReference.IDs));
            return Task.FromResult(validate(context));
        }
    }

    private static ReleaseCrossReferenceValidators Build(params IReleaseCrossReferenceValidator[] validators)
    {
        var runner = new ReleaseCrossReferenceValidators(NullLogger<ReleaseCrossReferenceValidators>.Instance);
        runner.AddParts(validators);
        return runner;
    }

    private static Task<IReadOnlyList<IReleaseVideoCrossReference>> Run(ReleaseCrossReferenceValidators runner, params IReleaseVideoCrossReference[] crossReferences)
        => runner.Validate(Mock.Of<IVideo>(), Mock.Of<IReleaseInfo>(), crossReferences, TestContext.Current.CancellationToken);

    private static Task<IReadOnlyList<IReleaseVideoCrossReference>> Heal(ReleaseCrossReferenceValidators runner, params IReleaseVideoCrossReference[] crossReferences)
        => runner.Validate(Mock.Of<IVideo>(), Mock.Of<IReleaseInfo>(), crossReferences, TestContext.Current.CancellationToken, heal: true);

    private static ReleaseVideoCrossReference Xref(params MetadataGuid[] ids)
        => new() { IDs = [.. ids] };

    private static ReleaseCrossReferenceValidationResult AddingID(IReleaseVideoCrossReference xref, MetadataGuid id)
        => ReleaseCrossReferenceValidationResult.Corrected([.. xref.IDs, id]);

    private static ReleaseCrossReferenceValidationResult WithRange(IReleaseVideoCrossReference xref, int start, int end)
        => ReleaseCrossReferenceValidationResult.Corrected(xref.IDs, start, end);

    private static ReleaseVideoCrossReference WithIDs(ReleaseVideoCrossReference xref, params MetadataGuid[] ids)
    {
        xref.IDs.AddRange(ids);
        return xref;
    }

    #endregion

    #region Claiming

    [Fact]
    public async Task AnIDNoValidatorTakes_RefusesTheRelease()
    {
        var runner = Build(new TestValidator("Episodes", MetadataEntityScope.Single(TestSources.Plugin, MetadataEntityType.Episode), _ => null));

        var ex = await Assert.ThrowsAsync<ReleaseValidationException>(() => Run(runner, Xref(_pluginEpisode, _pluginSeries)));

        Assert.Null(ex.ValidatorName);
        Assert.Contains("test-plugin://series", ex.Reason);
    }

    [Theory]
    [InlineData("season", false)]
    [InlineData("collection", false)]
    [InlineData("creator", false)]
    [InlineData("series", true)]
    [InlineData("episode", true)]
    [InlineData("movie", true)]
    public async Task OnlySeriesEpisodesAndMovies_MayBeLinked_WhateverAValidatorTakes(string kind, bool allowed)
    {
        var id = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Get(kind), "x1");
        var runner = Build(new TestValidator("Everything", MetadataEntityScope.ForSource(TestSources.Plugin, MetadataEntityType.Get(kind)), _ => null));

        var run = () => Run(runner, Xref(id));

        if (allowed)
            Assert.Single(await run());
        else
            Assert.Null((await Assert.ThrowsAsync<ReleaseValidationException>(run)).ValidatorName);
    }

    [Fact]
    public async Task ACrossReferenceNamingNothing_RefusesTheRelease()
    {
        var runner = Build(new TestValidator("Plugin", _pluginScope, _ => null));

        await Assert.ThrowsAsync<ReleaseValidationException>(() => Run(runner, Xref(_pluginEpisode), Xref()));
    }

    [Fact]
    public async Task AValidatorIsOnlyAskedAboutCrossReferencesInItsScope_AndNullLeavesThemAsTheyAre()
    {
        var plugin = new TestValidator("Plugin", _pluginScope, _ => null);
        var anidb = new TestValidator("AniDB", MetadataEntityScope.Single(MetadataSource.AniDB, MetadataEntityType.Episode), _ => null);
        var runner = Build(plugin, anidb);
        var anidbXref = ReleaseVideoCrossReference.ForAniDB(5);

        var result = await Run(runner, Xref(_pluginEpisode), anidbXref);

        Assert.Equal(["test-plugin://episode/e1"], plugin.Seen);
        Assert.Equal(["anidb://episode/5"], anidb.Seen);
        Assert.Same(anidbXref, result[1]);
    }

    #endregion

    #region Order and agreement

    [Fact]
    public async Task OverlappingValidators_RunInOrder_AndCorrectionsChain()
    {
        var first = new TestValidator("First", _pluginScope, context => AddingID(context.CrossReference, _pluginSeries));
        var second = new TestValidator("Second", _pluginScope, _ => ReleaseCrossReferenceValidationResult.Valid());
        var runner = Build(first, second);

        var result = await Run(runner, Xref(_pluginEpisode));

        Assert.Equal(["test-plugin://episode/e1"], first.Seen);
        Assert.Equal(["test-plugin://episode/e1,test-plugin://series/s1"], second.Seen);
        Assert.Equal([_pluginEpisode, _pluginSeries], Assert.Single(result).IDs);
    }

    [Fact]
    public async Task ALaterValidatorSeesTheEarlierCorrectionsOfTheWholeRelease()
    {
        var first = new TestValidator("First", _pluginScope, context => AddingID(context.CrossReference, _pluginSeries));
        var counts = new List<int>();
        var second = new TestValidator("Second", _pluginScope, context =>
        {
            counts.Add(context.CrossReferences.Count(xref => xref.IDs.Contains(_pluginSeries)));
            return null;
        });

        await Run(Build(first, second), Xref(_pluginEpisode), Xref(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Episode, "e2")));

        Assert.Equal([2, 2], counts);
    }

    [Fact]
    public async Task AllOverlappingValidatorsMustAgree_AndTheRefusalNamesTheValidator()
    {
        var later = new TestValidator("Later", _pluginScope, _ => ReleaseCrossReferenceValidationResult.Valid());
        var runner = Build(
            new TestValidator("Agrees", _pluginScope, _ => ReleaseCrossReferenceValidationResult.Valid()),
            new TestValidator("Refuses", _pluginScope, _ => ReleaseCrossReferenceValidationResult.Invalid("Not one of ours.")),
            later
        );

        var ex = await Assert.ThrowsAsync<ReleaseValidationException>(() => Run(runner, Xref(_pluginEpisode)));

        Assert.Equal("Refuses", ex.ValidatorName);
        Assert.Contains("Not one of ours.", ex.Reason);
        Assert.Empty(later.Seen);
    }

    [Fact]
    public async Task AnInvalidCrossReference_RefusesTheWholeRelease()
    {
        var runner = Build(new TestValidator("Plugin", _pluginScope, context => context.Index is 1 ? ReleaseCrossReferenceValidationResult.Invalid("Bad.") : null));

        await Assert.ThrowsAsync<ReleaseValidationException>(() => Run(runner, Xref(_pluginEpisode), Xref(_pluginSeries)));
    }

    #endregion

    #region Ranges

    [Fact]
    public async Task ARangeChange_IsCheckedAgainByTheValidatorsThatFoundItValid_AndKeptWhenTheyAgree()
    {
        var ranges = new List<string>();
        var anidb = new TestValidator("AniDB", MetadataEntityScope.Single(MetadataSource.AniDB, MetadataEntityType.Episode), context =>
        {
            ranges.Add($"{context.CrossReference.PercentageStart}-{context.CrossReference.PercentageEnd}");
            return ReleaseCrossReferenceValidationResult.Valid();
        });

        // The plugin's IDs sit beside an AniDB episode outside its scope, which does not stop it fixing the range.
        var plugin = new TestValidator("Plugin", _pluginScope, context => WithRange(context.CrossReference, 0, 50));
        var runner = Build(anidb, plugin);

        var result = Assert.Single(await Run(runner, WithIDs(ReleaseVideoCrossReference.ForAniDB(5), _pluginEpisode)));

        Assert.Equal(["0-100", "0-50"], ranges);
        Assert.Equal((0, 50), (result.PercentageStart, result.PercentageEnd));
        Assert.Equal(2, result.IDs.Count);
    }

    [Fact]
    public async Task AValidatorChangingTheRangeAgain_RefusesTheRelease_NamingBoth()
    {
        var anidb = new TestValidator("AniDB", MetadataEntityScope.Single(MetadataSource.AniDB, MetadataEntityType.Episode), context => WithRange(context.CrossReference, 0, 100));
        var plugin = new TestValidator("Plugin", _pluginScope, context => WithRange(context.CrossReference, 0, 50));
        var runner = Build(anidb, plugin);

        var ex = await Assert.ThrowsAsync<ReleaseValidationException>(() => Run(runner, WithIDs(ReleaseVideoCrossReference.ForAniDB(5), _pluginEpisode)));

        Assert.Equal("AniDB", ex.ValidatorName);
        Assert.Contains("Plugin", ex.Reason);
    }

    #endregion

    #region Healing

    [Fact]
    public async Task AHeal_AppliesTheCorrections_AndTellsTheValidatorsItIsOne()
    {
        var healing = new List<bool>();
        var runner = Build(new TestValidator("Plugin", _pluginScope, context =>
        {
            healing.Add(context.IsHealing);
            return AddingID(context.CrossReference, _pluginSeries);
        }));

        var result = await Heal(runner, Xref(_pluginEpisode));

        Assert.Equal([true], healing);
        Assert.Equal([_pluginEpisode, _pluginSeries], Assert.Single(result).IDs);
    }

    [Fact]
    public async Task AHeal_NeverRefuses_AndUndoesTheStepThatWould()
    {
        var episode2 = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Episode, "e2");
        var runner = Build(
            new TestValidator("Fills", _pluginScope, context => AddingID(context.CrossReference, _pluginSeries)),
            new TestValidator("Refuses", _pluginScope, context => context.Index is 0 ? ReleaseCrossReferenceValidationResult.Invalid("No.") : null),
            new TestValidator("Throws", _pluginScope, context => context.Index is 1 ? throw new InvalidOperationException() : null)
        );

        var result = await Heal(runner, Xref(_pluginEpisode), Xref(episode2));

        // The refusal and the throw undo nothing but their own steps.
        Assert.Equal([[_pluginEpisode, _pluginSeries], [episode2, _pluginSeries]], result.Select(xref => xref.IDs.ToList()));
    }

    [Fact]
    public async Task AHeal_LeavesACrossReferenceNoValidatorTakes_AsItIs()
    {
        var runner = Build(new TestValidator("Episodes", MetadataEntityScope.Single(TestSources.Plugin, MetadataEntityType.Episode), _ => null));
        var orphan = Xref(new MetadataGuid(TestSources.LocalPlugin, MetadataEntityType.Movie, "m1"));

        var result = await Heal(runner, orphan);

        Assert.Same(orphan, Assert.Single(result));
    }

    #endregion

    #region Failures

    [Fact]
    public async Task AValidatorThatThrows_RefusesTheRelease()
    {
        var runner = Build(new TestValidator("Broken", _pluginScope, _ => throw new InvalidCastException()));

        var ex = await Assert.ThrowsAsync<ReleaseValidationException>(() => Run(runner, Xref(_pluginEpisode)));

        Assert.Equal("Broken", ex.ValidatorName);
        Assert.IsType<InvalidCastException>(ex.InnerException);
    }

    [Fact]
    public async Task ACorrectionOutsideTheValidatorsScope_RefusesTheRelease()
    {
        var runner = Build(
            new TestValidator("Episodes", MetadataEntityScope.Single(TestSources.Plugin, MetadataEntityType.Episode), context => AddingID(context.CrossReference, _pluginSeries)),
            new TestValidator("Series", MetadataEntityScope.Single(TestSources.Plugin, MetadataEntityType.Series), _ => null)
        );

        var ex = await Assert.ThrowsAsync<ReleaseValidationException>(() => Run(runner, Xref(_pluginEpisode)));

        Assert.Equal("Episodes", ex.ValidatorName);
    }

    [Fact]
    public void AValidatorWithAnEmptyScope_IsNotTaken()
    {
        var runner = Build(new TestValidator("Empty", MetadataEntityScope.Empty, _ => null), new TestValidator("Plugin", _pluginScope, _ => null));

        Assert.Equal(["Plugin"], runner.Validators.Select(validator => validator.Name));
    }

    #endregion
}
