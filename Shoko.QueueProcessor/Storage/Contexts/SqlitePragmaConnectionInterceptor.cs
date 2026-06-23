using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Shoko.QueueProcessor.Storage.Contexts;

/// <summary>
/// Applies per-connection SQLite PRAGMAs every time a (pooled) connection is opened. These are set
/// on the live connection rather than via the connection string because the connection string is a
/// user-facing setting we don't want to depend on or rewrite.
/// <para>
/// <c>journal_mode=WAL</c> lets readers and writers proceed concurrently instead of blocking each
/// other under the default rollback journal. <c>busy_timeout</c> installs SQLite's native busy
/// handler so a contended writer waits for the lock to clear instead of immediately throwing
/// <c>SQLITE_BUSY</c> ("database is locked") — which previously escaped a worker thread and aborted
/// the process.
/// </para>
/// </summary>
public sealed class SqlitePragmaConnectionInterceptor : DbConnectionInterceptor
{
    // Generous enough to ride out a burst of concurrent writes from multiple workers plus the
    // background flush; matches Microsoft.Data.Sqlite's default command timeout.
    private const int BusyTimeoutMs = 30_000;

    private static readonly string PragmaSql = $"PRAGMA busy_timeout={BusyTimeoutMs}; PRAGMA journal_mode=WAL;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => Apply(connection);

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = PragmaSql;
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void Apply(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = PragmaSql;
        cmd.ExecuteNonQuery();
    }
}
