using System;

namespace Shoko.Server.Services.Mylist;

/// <summary>
/// The action to take for one entry, and the date it carries. For an import
/// that is the date to record locally, and for an export the date to send;
/// <c>null</c> in either direction means "not watched".
/// </summary>
public readonly record struct MylistWatchedAction(MylistWatchedActionKind Kind, DateTime? Date)
{
    public static readonly MylistWatchedAction None = new(MylistWatchedActionKind.None, null);

    public static MylistWatchedAction Import(DateTime? date) => new(MylistWatchedActionKind.Import, date);

    public static MylistWatchedAction Export(DateTime? date) => new(MylistWatchedActionKind.Export, date);
}
