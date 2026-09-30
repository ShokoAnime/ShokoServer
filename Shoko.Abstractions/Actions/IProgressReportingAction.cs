using System;

namespace Shoko.Abstractions.Actions;

/// <summary>
///   Opt-in interface for actions that report how far along they are while
///   they execute.
/// </summary>
/// <remarks>
///   The progress shows on the action's queue job. It is held in memory only,
///   and the job shows none until the action first reports, so report 0 as
///   soon as the action knows it will report.
/// </remarks>
public interface IProgressReportingAction
{
    /// <summary>
    ///   Sets the progress reporter. Called by the framework only, before
    ///   <see cref="IExecutableAction.Execute"/>.
    /// </summary>
    /// <param name="progress">
    ///   Takes how far the action is, as a percentage from 0 to 100. Values
    ///   outside that range are clamped.
    /// </param>
    void SetProgress(IProgress<decimal> progress);
}
