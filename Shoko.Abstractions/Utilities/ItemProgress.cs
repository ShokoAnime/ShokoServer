using System;
using System.Threading;

namespace Shoko.Abstractions.Utilities;

/// <summary>
///   Counts the items of a known amount of work done and reports the share
///   done to a parent progress, as a percentage from 0 to 100, as a
///   scheduled action's progress takes it.
/// </summary>
/// <remarks>
///   The count never goes down and never past <see cref="Total"/>, so the
///   parent never sees the progress go down. No items at all count as done.
///   Safe to use from several threads.
/// </remarks>
/// <example>
///   <code>
///   var items = new ItemProgress(progress, videos.Count);
///   items.Report(0);
///   foreach (var video in videos)
///   {
///       token.ThrowIfCancellationRequested();
///       await Process(video);
///       items.Increment();
///   }
///   </code>
/// </example>
public sealed class ItemProgress
{
    #region Fields

    private readonly IProgress<decimal>? _parent;

    private readonly Lock _lock = new();

    #endregion

    #region Constructors

    /// <summary>
    ///   Creates a counter for <paramref name="total"/> items. Reports
    ///   nothing until told to.
    /// </summary>
    /// <param name="parent">
    ///   Takes the share done, or <see langword="null"/> to drop it.
    /// </param>
    /// <param name="total">How many items there are, zero or more.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <paramref name="total"/> is negative.
    /// </exception>
    public ItemProgress(IProgress<decimal>? parent, int total)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(total);
        _parent = parent;
        Total = total;
    }

    #endregion

    #region Properties

    /// <summary>
    ///   How many items there are.
    /// </summary>
    public int Total { get; }

    /// <summary>
    ///   How many items are done.
    /// </summary>
    public int Done { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    ///   Counts <paramref name="count"/> more items as done and reports the
    ///   share done.
    /// </summary>
    /// <param name="count">How many more items are done, zero or more.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <paramref name="count"/> is negative.
    /// </exception>
    public void Increment(int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        lock (_lock)
            Forward((int)Math.Min((long)Done + count, Total));
    }

    /// <summary>
    ///   Sets how many items are done and reports the share done. A count
    ///   below the current one is ignored.
    /// </summary>
    /// <param name="done">How many items are done.</param>
    public void Report(int done)
    {
        lock (_lock)
        {
            if (done < Done)
                return;

            Forward(Math.Min(done, Total));
        }
    }

    /// <summary>
    ///   Stores the count and sends the share done to the parent. Called
    ///   under the lock.
    /// </summary>
    /// <param name="done">How many items are done.</param>
    private void Forward(int done)
    {
        Done = done;
        _parent?.Report(Total is 0 ? 100m : 100m * done / Total);
    }

    #endregion
}
