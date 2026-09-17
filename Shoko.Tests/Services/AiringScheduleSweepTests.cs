using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Plugin;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Models.Airing;
using Shoko.Server.Plugin;
using Shoko.Server.Repositories.Cached.Airing;
using Shoko.Server.Scheduling.Jobs.Airing;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   The core-driven sweep driver, which is everything around a provider's own walk: whether it is
///   swept at all, where it resumes, how a chunk that did not come back cleanly is classified, and
///   what survives a restart.
/// </summary>
/// <remarks>
///   The driver reaches its one row through <c>RepoFactory</c>, so the sweep state repository is
///   built over an in-memory cache and installed with <see cref="RepoFactoryScope"/>. Providers are
///   real implementations rather than mocks, because a defaulted interface member is part of what
///   is under test and Moq would fill those in itself.
/// </remarks>
[Collection(nameof(RepoFactoryCollection))]
public class AiringScheduleSweepTests
{
    #region The Cursor

    [Fact]
    public async Task ExecuteSweep_CallsTheProviderWithNoCursorOnTheFirstChunk()
    {
        var provider = new ScriptedProvider("Alpha", (_, _) => Task.FromResult<string?>(null));
        using var harness = new Harness(provider);

        await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), TestContext.Current.CancellationToken);

        Assert.Equal([null], provider.Cursors);
    }

    [Fact]
    public async Task ExecuteSweep_CallsTheProviderAgainWithTheCursorItReturned()
    {
        var provider = new ScriptedProvider("Alpha", (cursor, _) => Task.FromResult<string?>(cursor is null ? "page-2" : null));
        using var harness = new Harness(provider);

        var first = await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), TestContext.Current.CancellationToken);
        var second = await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), TestContext.Current.CancellationToken);

        Assert.Equal([null, "page-2"], provider.Cursors);
        Assert.False(first!.IsFinished);
        Assert.True(second!.IsFinished);
    }

    [Fact]
    public async Task ExecuteSweep_QueuesTheNextChunkOnlyWhileThereIsACursor()
    {
        var provider = new ScriptedProvider("Alpha", (cursor, _) => Task.FromResult<string?>(cursor is null ? "page-2" : null));
        using var harness = new Harness(provider);

        await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), TestContext.Current.CancellationToken);
        harness.VerifyContinuations(Times.Once());

        await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), TestContext.Current.CancellationToken);
        harness.VerifyContinuations(Times.Once());
    }

    [Fact]
    public async Task ExecuteSweep_ANullCursorEndsTheSweep()
    {
        var provider = new ScriptedProvider("Alpha", (_, _) => Task.FromResult<string?>(null));
        using var harness = new Harness(provider);

        var result = await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), TestContext.Current.CancellationToken);

        Assert.True(result!.IsFinished);
        Assert.Null(harness.StateOf(provider)!.Cursor);
        Assert.False(harness.StateOf(provider)!.IsSweeping);
        // Finished, so the next one waits out the provider's interval rather than starting now.
        Assert.Empty(harness.Service.GetDueSweepProviders(DateTime.UtcNow));
    }

    [Fact]
    public async Task ExecuteSweep_KeepsTheCursorVerbatimThroughTheStore()
    {
        const string cursor = """{"after":"12345","week":"2026-09-14T00:00:00Z","q":"あ / b\\c"}""";
        var provider = new ScriptedProvider("Alpha", (seen, _) => Task.FromResult<string?>(seen is null ? cursor : null));
        using var harness = new Harness(provider);

        await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), TestContext.Current.CancellationToken);
        Assert.Equal(cursor, harness.StateOf(provider)!.Cursor);

        await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), TestContext.Current.CancellationToken);
        Assert.Equal([null, cursor], provider.Cursors);
    }

    [Fact]
    public async Task ExecuteSweep_RefusesACursorTooLongToStoreAndEndsTheSweep()
    {
        var oversized = new string('x', AiringScheduleSweepState.MaxCursorLength + 1);
        var provider = new ScriptedProvider("Alpha", (_, _) => Task.FromResult<string?>(oversized));
        using var harness = new Harness(provider);

        var result = await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), TestContext.Current.CancellationToken);

        Assert.Equal(AiringScheduleSweepOutcome.Failed, result!.Outcome);
        Assert.True(result.IsFinished);
        Assert.Null(harness.StateOf(provider)!.Cursor);
        harness.VerifyContinuations(Times.Never());
    }

    #endregion

    #region Outcomes

    [Fact]
    public async Task ExecuteSweep_ReturningNormallyIsCompleted()
    {
        var provider = new ScriptedProvider("Alpha", (_, _) => Task.FromResult<string?>("more"));
        using var harness = new Harness(provider);

        var result = await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), TestContext.Current.CancellationToken);

        Assert.Equal(AiringScheduleSweepOutcome.Completed, result!.Outcome);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(AiringScheduleSweepOutcome.Completed, harness.StateOf(provider)!.LastOutcome);
    }

    [Fact]
    public async Task ExecuteSweep_TheDeadlineFiringIsTimedOutAndKeepsTheCursor()
    {
        var provider = new ScriptedProvider("Alpha", async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return null;
        });
        using var harness = new Harness(provider);
        // The budget is the server's, not the provider's, and this is the shortest it allows.
        harness.Settings.SweepBudgetSeconds = AiringScheduleServiceSettings.MinimumSweepBudgetSeconds;
        harness.SeedState(provider, "page-7", DateTime.UtcNow.AddDays(-1), AiringScheduleSweepOutcome.Completed);

        var result = await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), TestContext.Current.CancellationToken);

        Assert.Equal(AiringScheduleSweepOutcome.TimedOut, result!.Outcome);
        Assert.False(result.IsFinished);
        // Unchanged, so the next chunk starts where the last returned cursor left off.
        Assert.Equal("page-7", harness.StateOf(provider)!.Cursor);
        harness.VerifyContinuations(Times.Never());
    }

    [Fact]
    public async Task ExecuteSweep_TheWorkersTokenFiringIsStopped()
    {
        var provider = new ScriptedProvider("Alpha", (_, token) =>
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(null);
        });
        using var harness = new Harness(provider);
        harness.SeedState(provider, "page-7", DateTime.UtcNow.AddDays(-1), AiringScheduleSweepOutcome.Completed);
        using var worker = new CancellationTokenSource();
        await worker.CancelAsync();

        var result = await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), worker.Token);

        Assert.Equal(AiringScheduleSweepOutcome.Stopped, result!.Outcome);
        Assert.Equal("page-7", harness.StateOf(provider)!.Cursor);
    }

    [Fact]
    public async Task ExecuteSweep_TheProviderCancellingItselfIsCancelled()
    {
        var provider = new ScriptedProvider("Alpha", (_, _) => throw new OperationCanceledException());
        using var harness = new Harness(provider);

        var result = await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), TestContext.Current.CancellationToken);

        Assert.Equal(AiringScheduleSweepOutcome.Cancelled, result!.Outcome);
    }

    [Fact]
    public async Task ExecuteSweep_AnyOtherExceptionIsFailedAndIsNotRethrown()
    {
        var provider = new ScriptedProvider("Alpha", (_, _) => throw new InvalidOperationException("the source is down"));
        using var harness = new Harness(provider);

        var result = await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), TestContext.Current.CancellationToken);

        Assert.Equal(AiringScheduleSweepOutcome.Failed, result!.Outcome);
        Assert.Equal("the source is down", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteSweep_DispatchesOneEventPerChunk()
    {
        var provider = new ScriptedProvider("Alpha", (cursor, _) => Task.FromResult<string?>(cursor is null ? "page-2" : null));
        using var harness = new Harness(provider);
        var events = new List<AiringScheduleSweepEventArgs>();
        harness.Service.SweepCompleted += (_, args) => events.Add(args);

        await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), TestContext.Current.CancellationToken);
        await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), TestContext.Current.CancellationToken);

        Assert.Equal(2, events.Count);
        Assert.All(events, args =>
        {
            Assert.Equal(harness.IDOf(provider), args.Provider.ID);
            Assert.Equal("Alpha", args.Provider.Name);
            Assert.True(args.CompletedAt >= args.StartedAt);
            Assert.Equal(args.CompletedAt - args.StartedAt, args.Duration);
        });
        Assert.Equal([false, true], events.Select(args => args.IsFinished));
    }

    #endregion

    #region Enablement

    [Fact]
    public async Task ExecuteSweep_SkipsADisabledProvider()
    {
        var provider = new ScriptedProvider("Alpha", (_, _) => Task.FromResult<string?>(null));
        using var harness = new Harness(provider);
        harness.Disable(provider);

        var result = await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Empty(provider.Cursors);
        Assert.Null(harness.StateOf(provider));
    }

    [Fact]
    public void GetDueSweepProviders_SkipsADisabledProvider()
    {
        var enabled = new ScriptedProvider("Alpha", (_, _) => Task.FromResult<string?>(null));
        var disabled = new SecondScriptedProvider("Beta", (_, _) => Task.FromResult<string?>(null));
        using var harness = new Harness(enabled, disabled);
        harness.Disable(disabled);

        var due = harness.Service.GetDueSweepProviders(DateTime.UtcNow);

        Assert.Equal([harness.IDOf(enabled)], due.Select(info => info.ID));
    }

    [Fact]
    public void GetDueSweepProviders_SkipsAProviderThatDoesNotSweep()
    {
        var sweeping = new ScriptedProvider("Alpha", (_, _) => Task.FromResult<string?>(null));
        var plain = new PlainProvider("Beta");
        using var harness = new Harness(sweeping, plain);

        var due = harness.Service.GetDueSweepProviders(DateTime.UtcNow);

        Assert.Equal([harness.IDOf(sweeping)], due.Select(info => info.ID));
    }

    [Fact]
    public async Task ExecuteSweep_SkipsAProviderThatDoesNotSweep()
    {
        var plain = new PlainProvider("Beta");
        using var harness = new Harness(plain);

        Assert.Null(await harness.Service.ExecuteSweepAsync(harness.IDOf(plain), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteSweep_SkipsAnUnregisteredProvider()
    {
        var provider = new ScriptedProvider("Alpha", (_, _) => Task.FromResult<string?>(null));
        using var harness = new Harness(provider);

        Assert.Null(await harness.Service.ExecuteSweepAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    #endregion

    #region No Progress

    [Fact]
    public async Task ExecuteSweep_HandingBackTheSameCursorIsNoProgressAndQueuesNothing()
    {
        var provider = new ScriptedProvider("Alpha", (cursor, _) => Task.FromResult<string?>(cursor ?? "page-2"));
        using var harness = new Harness(provider);

        await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), TestContext.Current.CancellationToken);
        Assert.Equal(0, harness.StateOf(provider)!.NoProgressCount);
        harness.VerifyContinuations(Times.Once());

        await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.StateOf(provider)!.NoProgressCount);
        // Still the one from the chunk that actually moved, so this did not spin.
        harness.VerifyContinuations(Times.Once());
    }

    [Fact]
    public async Task ExecuteSweep_MovingOnAgainResetsTheNoProgressCount()
    {
        var provider = new ScriptedProvider("Alpha", (_, _) => Task.FromResult<string?>("page-3"));
        using var harness = new Harness(provider);
        harness.SeedState(provider, "page-2", DateTime.UtcNow, AiringScheduleSweepOutcome.TimedOut, noProgressCount: 2);

        await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), TestContext.Current.CancellationToken);

        Assert.Equal(0, harness.StateOf(provider)!.NoProgressCount);
    }

    [Fact]
    public async Task ExecuteSweep_FinishingIsProgressHoweverTheCursorReads()
    {
        var provider = new ScriptedProvider("Alpha", (_, _) => Task.FromResult<string?>(null));
        using var harness = new Harness(provider);
        harness.SeedState(provider, null, DateTime.UtcNow.AddDays(-2), AiringScheduleSweepOutcome.Completed, noProgressCount: 2);

        await harness.Service.ExecuteSweepAsync(harness.IDOf(provider), TestContext.Current.CancellationToken);

        Assert.Equal(0, harness.StateOf(provider)!.NoProgressCount);
    }

    [Fact]
    public void GetDueSweepProviders_HoldsOffASweepThatKeepsGettingNowhere()
    {
        var provider = new ScriptedProvider("Alpha", (cursor, _) => Task.FromResult<string?>(cursor)) { Suggested = TimeSpan.FromHours(6) };
        using var harness = new Harness(provider);
        harness.SeedState(provider, "page-2", DateTime.UtcNow, AiringScheduleSweepOutcome.TimedOut, AiringScheduleService.MaxNoProgressChunks - 1);
        Assert.Single(harness.Service.GetDueSweepProviders(DateTime.UtcNow));

        harness.SeedState(provider, "page-2", DateTime.UtcNow, AiringScheduleSweepOutcome.TimedOut, AiringScheduleService.MaxNoProgressChunks);

        Assert.Empty(harness.Service.GetDueSweepProviders(DateTime.UtcNow));
        // Held back to the interval rather than abandoned, so it picks up again later.
        Assert.Single(harness.Service.GetDueSweepProviders(DateTime.UtcNow.AddHours(7)));
    }

    #endregion

    #region The Interval

    [Fact]
    public void AddParts_SeedsTheIntervalFromTheProvidersSuggestion()
    {
        var provider = new ScriptedProvider("Alpha", (_, _) => Task.FromResult<string?>(null)) { Suggested = TimeSpan.FromHours(6) };
        using var harness = new Harness(provider);

        Assert.Equal(TimeSpan.FromHours(6), harness.Service.GetProviderInfo(provider).SweepInterval);
    }

    [Fact]
    public void AddParts_FallsBackToTheServersOwnIntervalWhenNothingIsSuggested()
    {
        var provider = new ScriptedProvider("Alpha", (_, _) => Task.FromResult<string?>(null));
        using var harness = new Harness(provider);

        Assert.Equal(AiringScheduleServiceSettings.DefaultSweepInterval, harness.Service.GetProviderInfo(provider).SweepInterval);
    }

    [Fact]
    public void UpdateProviders_TheUsersIntervalWinsOverTheSuggestionAndIsClamped()
    {
        var provider = new ScriptedProvider("Alpha", (_, _) => Task.FromResult<string?>(null)) { Suggested = TimeSpan.FromHours(6) };
        using var harness = new Harness(provider);

        var info = harness.Service.GetProviderInfo(provider);
        info.SweepInterval = TimeSpan.FromHours(2);
        harness.Service.UpdateProviders(info);
        Assert.Equal(TimeSpan.FromHours(2), harness.Service.GetProviderInfo(provider).SweepInterval);
        Assert.Equal(TimeSpan.FromHours(2), harness.Settings.SweepIntervals[harness.IDOf(provider)]);

        info = harness.Service.GetProviderInfo(provider);
        info.SweepInterval = TimeSpan.FromSeconds(1);
        harness.Service.UpdateProviders(info);

        Assert.Equal(AiringScheduleServiceSettings.MinimumSweepInterval, harness.Service.GetProviderInfo(provider).SweepInterval);
    }

    #endregion

    #region Due

    [Fact]
    public void GetDueSweepProviders_SweepsAProviderThatHasNeverSwept()
    {
        var provider = new ScriptedProvider("Alpha", (_, _) => Task.FromResult<string?>(null));
        using var harness = new Harness(provider);

        Assert.Single(harness.Service.GetDueSweepProviders(DateTime.UtcNow));
    }

    [Fact]
    public void GetDueSweepProviders_ResumesASweepCoreCutShortAtOnce()
    {
        var provider = new ScriptedProvider("Alpha", (_, _) => Task.FromResult<string?>(null));
        using var harness = new Harness(provider);
        harness.SeedState(provider, "page-2", DateTime.UtcNow, AiringScheduleSweepOutcome.TimedOut);

        Assert.Single(harness.Service.GetDueSweepProviders(DateTime.UtcNow));
    }

    [Fact]
    public void GetDueSweepProviders_HoldsOffASweepTheProviderEndedBadlyUntilTheIntervalPasses()
    {
        var provider = new ScriptedProvider("Alpha", (_, _) => Task.FromResult<string?>(null)) { Suggested = TimeSpan.FromHours(6) };
        using var harness = new Harness(provider);
        harness.SeedState(provider, "page-2", DateTime.UtcNow, AiringScheduleSweepOutcome.Failed);

        Assert.Empty(harness.Service.GetDueSweepProviders(DateTime.UtcNow));
        Assert.Single(harness.Service.GetDueSweepProviders(DateTime.UtcNow.AddHours(7)));
    }

    [Fact]
    public void GetDueSweepProviders_HoldsOffAFinishedSweepUntilTheIntervalPasses()
    {
        var provider = new ScriptedProvider("Alpha", (_, _) => Task.FromResult<string?>(null)) { Suggested = TimeSpan.FromHours(6) };
        using var harness = new Harness(provider);
        harness.SeedState(provider, null, DateTime.UtcNow, AiringScheduleSweepOutcome.Completed);

        Assert.Empty(harness.Service.GetDueSweepProviders(DateTime.UtcNow.AddHours(5)));
        Assert.Single(harness.Service.GetDueSweepProviders(DateTime.UtcNow.AddHours(7)));
    }

    [Fact]
    public void GetDueSweepProviders_ClampsAnImpatientInterval()
    {
        var provider = new ScriptedProvider("Alpha", (_, _) => Task.FromResult<string?>(null)) { Suggested = TimeSpan.FromSeconds(1) };
        using var harness = new Harness(provider);
        harness.SeedState(provider, null, DateTime.UtcNow, AiringScheduleSweepOutcome.Completed);

        Assert.Empty(harness.Service.GetDueSweepProviders(DateTime.UtcNow.AddMinutes(5)));
        Assert.Single(harness.Service.GetDueSweepProviders(DateTime.UtcNow.AddMinutes(16)));
    }

    [Fact]
    public async Task ScheduleSweeps_QueuesOneChunkPerDueProvider()
    {
        var first = new ScriptedProvider("Alpha", (_, _) => Task.FromResult<string?>(null));
        var second = new SecondScriptedProvider("Beta", (_, _) => Task.FromResult<string?>(null));
        using var harness = new Harness(first, second);
        harness.Disable(second);

        await harness.Service.ScheduleSweeps(TestContext.Current.CancellationToken);

        harness.Scheduler.Verify(
            scheduler => scheduler.Enqueue(
                It.IsAny<Action<SweepAiringScheduleProviderJob>>(),
                It.IsAny<bool>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    #endregion

    #region Harness

    /// <summary>
    ///   The sweep state repository installed into <c>RepoFactory</c>, a service with the given
    ///   providers registered, and a mocked queue to watch the continuations land in.
    /// </summary>
    private sealed class Harness : IDisposable
    {
        private readonly RepoFactoryScope _scope = new();

        private int _nextStateID = 1;

        public Mock<AiringScheduleSweepStateRepository> States { get; }

        public Mock<IQueueScheduler> Scheduler { get; } = new();

        public AiringScheduleServiceSettings Settings { get; } = new();

        public AiringScheduleService Service { get; }

        public Harness(params IAiringScheduleProvider[] providers)
        {
            States = CachedRepo.BuildWritable<AiringScheduleSweepStateRepository, int, AiringScheduleSweepState>(entry => entry.AiringScheduleSweepStateID);
            States.Setup(repository => repository.Save(It.IsAny<AiringScheduleSweepState>())).Callback<AiringScheduleSweepState>(entry =>
            {
                if (entry.AiringScheduleSweepStateID is 0)
                    entry.AiringScheduleSweepStateID = _nextStateID++;
                States.Object.Cache.Update(entry);
            });
            _scope.Set(States.Object);

            var configurationInfo = (ConfigurationInfo)RuntimeHelpers.GetUninitializedObject(typeof(ConfigurationInfo));
            var configurationService = new Mock<IConfigurationService>();
            configurationService.Setup(service => service.GetConfigurationInfo<AiringScheduleServiceSettings>()).Returns(configurationInfo);
            configurationService.Setup(service => service.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>())).Returns(Settings);
            var pluginManager = new Mock<IPluginManager>();
            pluginManager.Setup(manager => manager.GetPluginInfo(It.IsAny<Assembly>()))
                .Returns(PluginTestDoubles.CorePluginInfo(typeof(CorePlugin), CorePlugin.StaticID));

            Service = new AiringScheduleService(
                NullLogger<AiringScheduleService>.Instance,
                configurationService.Object,
                pluginManager.Object,
                Scheduler.Object,
                new ConfigurationProvider<AiringScheduleServiceSettings>(configurationService.Object)
            );
            Service.AddParts(providers, []);
        }

        /// <summary>The ID core registered the provider under.</summary>
        public Guid IDOf(IAiringScheduleProvider provider)
            => Service.GetProviderInfo(provider).ID;

        /// <summary>The provider's stored sweep state, or <c>null</c> when it has never swept.</summary>
        public AiringScheduleSweepState? StateOf(IAiringScheduleProvider provider)
            => States.Object.GetByProviderID(IDOf(provider));

        /// <summary>Pretends a chunk already ran and left the sweep in the given state.</summary>
        public void SeedState(IAiringScheduleProvider provider, string? cursor, DateTime lastRunAt, AiringScheduleSweepOutcome outcome, int noProgressCount = 0)
        {
            // One row per provider, so re-seeding edits the row rather than adding a second.
            var state = StateOf(provider) ?? new AiringScheduleSweepState() { ProviderID = IDOf(provider) };
            state.Cursor = cursor;
            state.LastRunAt = lastRunAt;
            state.LastOutcome = outcome;
            state.NoProgressCount = noProgressCount;
            States.Object.Save(state);
        }

        /// <summary>Turns every kind off, which is the only thing "disabled" means here.</summary>
        public void Disable(IAiringScheduleProvider provider)
        {
            var info = Service.GetProviderInfo(provider);
            info.EnabledKinds = [];
            Service.UpdateProviders(info);
        }

        /// <summary>Asserts how many follow-up chunks the driver queued.</summary>
        public void VerifyContinuations(Times times)
            => Scheduler.Verify(
                scheduler => scheduler.RunAfterCurrent(It.IsAny<Action<SweepAiringScheduleProviderJob>>(), It.IsAny<CancellationToken>()),
                times
            );

        public void Dispose()
            => _scope.Dispose();
    }

    /// <summary>A sweeping provider whose chunk is whatever the test handed it.</summary>
    private class ScriptedProvider(string name, Func<string?, CancellationToken, Task<string?>> sweep) : ISweepingAiringScheduleProvider
    {
        /// <summary>Every cursor the driver called this provider with, in order.</summary>
        public List<string?> Cursors { get; } = [];

        /// <summary>What the provider suggests, which core only seeds the user's value from.</summary>
        public TimeSpan? Suggested { get; init; }

        public string Name { get; } = name;

        public IReadOnlySet<AiringKind> AvailableKinds { get; } = new HashSet<AiringKind> { AiringKind.Original };

        public TimeSpan? SuggestedSweepInterval => Suggested;

        public Task<bool> RefreshAsync(ISeries series, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<string?> SweepAsync(string? cursor, CancellationToken cancellationToken)
        {
            Cursors.Add(cursor);
            return sweep(cursor, cancellationToken);
        }
    }

    /// <summary>
    ///   A second sweeping provider. Its own type, because a provider's ID is derived from its
    ///   type and two of one type would collide.
    /// </summary>
    private sealed class SecondScriptedProvider(string name, Func<string?, CancellationToken, Task<string?>> sweep) : ScriptedProvider(name, sweep);

    /// <summary>A provider that did not opt in, and is therefore never swept.</summary>
    private sealed class PlainProvider(string name) : IAiringScheduleProvider
    {
        public string Name { get; } = name;

        public IReadOnlySet<AiringKind> AvailableKinds { get; } = new HashSet<AiringKind> { AiringKind.Original };

        public Task<bool> RefreshAsync(ISeries series, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    #endregion
}
