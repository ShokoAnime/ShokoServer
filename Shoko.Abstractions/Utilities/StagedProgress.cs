using System;
using System.Linq;
using System.Threading;

namespace Shoko.Abstractions.Utilities;

/// <summary>
///   Splits a parent progress into stages run one after another, each
///   taking a share of 0 to 100 by its weight. All values are percentages
///   from 0 to 100, as a scheduled action's progress takes them.
/// </summary>
/// <remarks>
///   <see cref="Report"/> takes the progress within the current stage, so
///   the instance itself can be handed to the work of each stage. Values
///   are clamped, and a value not above one already reported is dropped, so
///   the parent never sees the progress go down. Safe to use from several
///   threads.
/// </remarks>
/// <example>
///   <code>
///   var stages = new StagedProgress(progress, 1, 3);
///   stages.Report(0);
///   await FindTheWork(stages, token);
///   stages.NextStage();
///   await DoTheWork(stages, token);
///   stages.Complete();
///   </code>
/// </example>
public sealed class StagedProgress : IProgress<decimal>
{
    #region Fields

    private readonly IProgress<decimal>? _parent;

    // Where each stage starts, then 100.
    private readonly decimal[] _bounds;

    private readonly Lock _lock = new();

    private decimal? _last;

    #endregion

    #region Constructors

    /// <summary>
    ///   Creates stages of equal weight.
    /// </summary>
    /// <param name="parent">
    ///   Takes the overall progress, or <c>null</c> to drop it.
    /// </param>
    /// <param name="stageCount">How many stages, at least one.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <paramref name="stageCount"/> is below one.
    /// </exception>
    public StagedProgress(IProgress<decimal>? parent, int stageCount)
        : this(parent, CreateEqualWeights(stageCount)) { }

    /// <summary>
    ///   Creates one stage per weight, each taking its weight's share of the
    ///   whole.
    /// </summary>
    /// <param name="parent">
    ///   Takes the overall progress, or <c>null</c> to drop it.
    /// </param>
    /// <param name="weights">
    ///   The stages' weights, none below zero and at least one above it.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="weights"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="weights"/> is empty, has a negative weight, or adds
    ///   up to zero.
    /// </exception>
    public StagedProgress(IProgress<decimal>? parent, params decimal[] weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        if (weights.Length is 0)
            throw new ArgumentException("At least one stage is needed.", nameof(weights));
        if (weights.Any(weight => weight < 0m))
            throw new ArgumentException("A stage's weight cannot be negative.", nameof(weights));

        var total = weights.Sum();
        if (total <= 0m)
            throw new ArgumentException("The weights must add up to more than zero.", nameof(weights));

        _parent = parent;
        _bounds = new decimal[weights.Length + 1];
        var sum = 0m;
        for (var index = 0; index < weights.Length; index++)
        {
            _bounds[index] = 100m * sum / total;
            sum += weights[index];
        }

        _bounds[^1] = 100m;
    }

    #endregion

    #region Properties

    /// <summary>
    ///   How many stages there are.
    /// </summary>
    public int StageCount => _bounds.Length - 1;

    /// <summary>
    ///   The index of the current stage, from 0, or <see cref="StageCount"/>
    ///   once every stage is done.
    /// </summary>
    public int CurrentStage { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    ///   Reports how far the current stage is. Ignored once every stage is
    ///   done.
    /// </summary>
    /// <param name="value">The progress within the stage, from 0 to 100.</param>
    public void Report(decimal value)
    {
        lock (_lock)
        {
            if (CurrentStage >= StageCount)
                return;

            var start = _bounds[CurrentStage];
            var end = _bounds[CurrentStage + 1];
            Forward(start + ((end - start) * Math.Clamp(value, 0m, 100m) / 100m));
        }
    }

    /// <summary>
    ///   Ends the current stage and starts the next, reporting where it
    ///   starts. Once past the last stage it reports 100.
    /// </summary>
    /// <param name="count">How many stages to move on, at least one.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <paramref name="count"/> is below one.
    /// </exception>
    public void NextStage(int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        lock (_lock)
        {
            CurrentStage = Math.Min(CurrentStage + count, StageCount);
            Forward(_bounds[CurrentStage]);
        }
    }

    /// <summary>
    ///   Ends every stage left and reports 100.
    /// </summary>
    public void Complete()
    {
        lock (_lock)
        {
            CurrentStage = StageCount;
            Forward(100m);
        }
    }

    /// <summary>
    ///   Sends <paramref name="value"/> to the parent, unless it or a higher
    ///   one already went. Called under the lock.
    /// </summary>
    /// <param name="value">The overall progress.</param>
    private void Forward(decimal value)
    {
        if (_last is { } last && value <= last)
            return;

        _last = value;
        _parent?.Report(value);
    }

    /// <summary>
    ///   Makes equal weights for <paramref name="stageCount"/> stages.
    /// </summary>
    /// <param name="stageCount">How many stages.</param>
    /// <returns>The weights.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <paramref name="stageCount"/> is below one.
    /// </exception>
    private static decimal[] CreateEqualWeights(int stageCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stageCount, 1);
        return Enumerable.Repeat(1m, stageCount).ToArray();
    }

    #endregion
}
