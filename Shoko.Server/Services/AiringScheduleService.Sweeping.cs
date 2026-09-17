using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Server.Models.Airing;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Jobs.Airing;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Services;

public partial class AiringScheduleService
{
    #region Sweeping

    /// <summary>
    /// How many chunks in a row may get nowhere before the sweep is held back
    /// to its interval instead of resuming at the next tick.
    /// </summary>
    /// <remarks>
    /// A deadline brings a failure a count does not: a provider that
    /// cannot finish one unit of its walk inside a whole chunk hands back the
    /// cursor it was given, and without this it would be called again, and
    /// again, burning a worker every tick and never moving. Three in a row is
    /// enough to tell a chunk that was merely unlucky from a source that is too
    /// slow to sweep at all.
    /// </remarks>
    internal const int MaxNoProgressChunks = 3;

    /// <inheritdoc/>
    public event EventHandler<AiringScheduleSweepEventArgs>? SweepCompleted;

    /// <summary>
    /// Enqueue a chunk for every sweeping provider that is due one. This is
    /// what the recurring dispatcher calls; the jobs are keyed by provider, so
    /// a tick that lands while a provider's chunk is still queued is a no-op
    /// for that provider.
    /// </summary>
    /// <param name="cancellationToken">The token cancelling the enqueue.</param>
    /// <returns>A task that completes once the chunks are queued.</returns>
    /// <exception cref="InvalidOperationException">Parts have not been added yet.</exception>
    internal async Task ScheduleSweeps(CancellationToken cancellationToken = default)
    {
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        foreach (var info in GetDueSweepProviders(DateTime.UtcNow))
        {
            logger.LogDebug("Queueing a sweep with provider {ProviderName}.", info.Name);
            await schedulerFactory.Enqueue<SweepAiringScheduleProviderJob>(job => job.ProviderID = info.ID, ct: cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The sweeping providers that want a chunk run now.
    /// </summary>
    /// <remarks>
    /// Enablement is core's gate and it lives here rather than in a provider,
    /// so a plugin author cannot forget it: a provider with no enabled kinds is
    /// disabled, exactly as it is for every other read and write, and is not
    /// swept at all. A provider whose kinds are merely narrowed is still swept,
    /// and is expected to check its own
    /// <see cref="AiringScheduleProviderInfo.EnabledKinds"/> before fetching a
    /// kind nobody asked for.
    /// </remarks>
    /// <param name="now">The current time, in UTC.</param>
    /// <returns>The providers to sweep, in source order.</returns>
    internal IReadOnlyList<AiringScheduleProviderInfo> GetDueSweepProviders(DateTime now)
        => GetAvailableProviders(onlyEnabled: true)
            .Where(info => info.Provider is ISweepingAiringScheduleProvider && IsSweepDue(info, now))
            .ToList();

    /// <summary>
    /// Whether the provider's next chunk is due.
    /// </summary>
    /// <remarks>
    /// A sweep core cut short resumes at the next tick, since nothing about it
    /// was the provider's decision, and so does one that is simply long. One
    /// the provider ended itself, well or badly, and one that has got nowhere
    /// <see cref="MaxNoProgressChunks"/> times over, waits out the whole
    /// interval instead, so neither a source that is down nor one that is too
    /// slow to finish a chunk is knocked on every tick.
    /// </remarks>
    /// <param name="info">The provider.</param>
    /// <param name="now">The current time, in UTC.</param>
    /// <returns>Whether to sweep.</returns>
    private static bool IsSweepDue(AiringScheduleProviderInfo info, DateTime now)
    {
        if (RepoFactory.AiringScheduleSweepState.GetByProviderID(info.ID) is not { } state)
            return true;

        if (state.IsSweeping &&
            state.NoProgressCount < MaxNoProgressChunks &&
            state.LastOutcome is AiringScheduleSweepOutcome.Completed or AiringScheduleSweepOutcome.TimedOut or AiringScheduleSweepOutcome.Stopped)
            return true;

        return now - state.LastRunAt >= info.SweepInterval;
    }

    /// <summary>
    /// Run one chunk of one provider's sweep. This is what the sweep job calls;
    /// providers implement only the walk.
    /// </summary>
    /// <remarks>
    /// The provider observes exactly one token: a source cancelled at this
    /// chunk's deadline, linked to <paramref name="workerToken"/> so shutdown
    /// cancels it too. Which of the two fired is what the outcome is worked out
    /// from afterwards, and that is best effort rather than exact, since a
    /// provider cancelling for its own reason in the same instant the deadline
    /// passes is indistinguishable from one the deadline caught.
    /// </remarks>
    /// <param name="providerID">The provider to sweep with.</param>
    /// <param name="workerToken">The worker's own token, cancelled on shutdown.</param>
    /// <returns>What the chunk did, or <c>null</c> when there was nothing to sweep.</returns>
    /// <exception cref="InvalidOperationException">Parts have not been added yet.</exception>
    internal async Task<AiringScheduleSweepEventArgs?> ExecuteSweepAsync(Guid providerID, CancellationToken workerToken = default)
    {
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        if (GetProviderInfo(providerID) is not { } info)
        {
            logger.LogWarning("Skipped a sweep for the unregistered provider {ProviderID}.", providerID);
            return null;
        }

        if (info.Provider is not ISweepingAiringScheduleProvider provider)
        {
            logger.LogWarning("Skipped a sweep for provider {ProviderName}, which does not sweep.", info.Name);
            return null;
        }

        // Checked again here and not only when the chunk was queued, so a
        // provider disabled while its job waited is not swept anyway.
        if (!info.Enabled)
        {
            logger.LogDebug("Skipped a sweep for the disabled provider {ProviderName}.", info.Name);
            return null;
        }

        var state = RepoFactory.AiringScheduleSweepState.GetByProviderID(providerID);
        var cursor = state?.Cursor;
        var previousNoProgress = state?.NoProgressCount ?? 0;
        var startedAt = DateTime.UtcNow;
        var outcome = AiringScheduleSweepOutcome.Completed;
        var nextCursor = cursor;
        string? errorMessage = null;
        var budget = GetSweepBudget();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(workerToken);
        deadline.CancelAfter(budget);
        try
        {
            nextCursor = await provider.SweepAsync(cursor, deadline.Token).ConfigureAwait(false);
            if (nextCursor is { } returned && returned.Length > AiringScheduleSweepState.MaxCursorLength)
            {
                // Nothing can be stored, so resuming would repeat this chunk
                // forever. End the sweep instead and let the interval restart it.
                outcome = AiringScheduleSweepOutcome.Failed;
                errorMessage = $"The cursor is {returned.Length} characters long, and at most {AiringScheduleSweepState.MaxCursorLength} can be stored.";
                nextCursor = null;
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown first: the deadline source is linked to the worker's
            // token, so it reads as cancelled either way, and a stopped sweep
            // is not held against the provider.
            outcome = workerToken.IsCancellationRequested
                ? AiringScheduleSweepOutcome.Stopped
                : deadline.IsCancellationRequested
                    ? AiringScheduleSweepOutcome.TimedOut
                    : AiringScheduleSweepOutcome.Cancelled;
            nextCursor = cursor;
        }
        catch (Exception ex)
        {
            // A provider that fails is reported, never thrown, so one source
            // going down doesn't stop the others being swept.
            outcome = AiringScheduleSweepOutcome.Failed;
            errorMessage = ex.Message;
            nextCursor = cursor;
            logger.LogError(ex, "Provider {ProviderName} failed to sweep.", info.Name);
        }

        var completedAt = DateTime.UtcNow;
        var advanced = outcome is AiringScheduleSweepOutcome.Completed && !string.Equals(nextCursor, cursor, StringComparison.Ordinal);
        var noProgressCount = outcome switch
        {
            // A sweep that reached its end got somewhere, whatever the cursor reads.
            AiringScheduleSweepOutcome.Completed => advanced || nextCursor is null ? 0 : previousNoProgress + 1,
            // A whole chunk's budget spent without finishing one unit of the walk.
            AiringScheduleSweepOutcome.TimedOut => previousNoProgress + 1,
            // Shutdown, a provider cancelling itself and a provider throwing are
            // all judged on their own, and none of them says anything about
            // whether the walk can move, so the count is left as it was.
            _ => previousNoProgress,
        };
        SaveSweepState(providerID, nextCursor, completedAt, outcome, noProgressCount);

        var args = new AiringScheduleSweepEventArgs()
        {
            Provider = info,
            Outcome = outcome,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            IsFinished = nextCursor is null,
            ErrorMessage = errorMessage,
        };
        if (outcome is AiringScheduleSweepOutcome.Failed)
            logger.LogError(
                "Swept airing schedules with provider {ProviderName} for {Duration:0.###}s: {Outcome}, and the sweep was abandoned. {ErrorMessage}",
                info.Name,
                args.Duration.TotalSeconds,
                outcome,
                errorMessage
            );
        else
            logger.LogInformation(
                "Swept airing schedules with provider {ProviderName} for {Duration:0.###}s: {Outcome}, and the sweep {Progress}.",
                info.Name,
                args.Duration.TotalSeconds,
                outcome,
                args.IsFinished ? "is finished" : "resumes"
            );

        if (noProgressCount > previousNoProgress)
            logger.LogWarning(
                "Provider {ProviderName} got nowhere in {NoProgressCount} chunk(s) in a row, and is still at the same place in its walk. " +
                "{Reason} {Consequence}",
                info.Name,
                noProgressCount,
                outcome is AiringScheduleSweepOutcome.TimedOut
                    ? $"It did not finish a single unit within the {budget.TotalSeconds:0.###}s a chunk is given, so it is too slow to sweep rather than stuck."
                    : "It returned the cursor it was handed, so it is stuck rather than slow.",
                noProgressCount >= MaxNoProgressChunks
                    ? $"The sweep is held back to the provider's own interval of {info.SweepInterval} until it moves again."
                    : "The sweep carries on for now."
            );

        SweepCompleted?.Invoke(this, args);

        // Only a chunk that came back of its own accord having actually moved
        // carries straight on. Everything else waits for the dispatcher, which
        // is what keeps a provider that overruns its deadline, or hands back the
        // cursor it was given, to one chunk a tick instead of a tight loop.
        if (advanced && nextCursor is not null)
            await schedulerFactory.RunAfterCurrent<SweepAiringScheduleProviderJob>(job => job.ProviderID = providerID).ConfigureAwait(false);

        return args;
    }

    /// <summary>
    /// Write back where the sweep got to.
    /// </summary>
    /// <param name="providerID">The provider that was swept.</param>
    /// <param name="cursor">Where the next chunk resumes, or <c>null</c> when the sweep is over.</param>
    /// <param name="completedAt">When the chunk finished, in UTC.</param>
    /// <param name="outcome">How the chunk ended.</param>
    /// <param name="noProgressCount">How many chunks in a row have got nowhere.</param>
    private static void SaveSweepState(Guid providerID, string? cursor, DateTime completedAt, AiringScheduleSweepOutcome outcome, int noProgressCount)
    {
        var state = RepoFactory.AiringScheduleSweepState.GetByProviderID(providerID) ?? new() { ProviderID = providerID };
        state.Cursor = cursor;
        state.LastRunAt = completedAt;
        state.LastOutcome = outcome;
        state.NoProgressCount = noProgressCount;
        RepoFactory.AiringScheduleSweepState.Save(state);
    }

    /// <summary>
    /// How long one chunk may run. This is the server's own, never a
    /// provider's, because the chunk is holding a queue worker while it runs.
    /// </summary>
    /// <remarks>
    /// The one place the configured budget is clamped, and so also what the
    /// queue watchdog's threshold for the sweep job is worked out from.
    /// </remarks>
    /// <returns>The deadline.</returns>
    internal TimeSpan GetSweepBudget()
        => TimeSpan.FromSeconds(Math.Clamp(
            configurationProvider.Load().SweepBudgetSeconds,
            AiringScheduleServiceSettings.MinimumSweepBudgetSeconds,
            AiringScheduleServiceSettings.MaximumSweepBudgetSeconds
        ));

    #endregion
}
