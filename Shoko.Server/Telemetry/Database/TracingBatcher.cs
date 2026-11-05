using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using NHibernate;
using NHibernate.AdoNet;
using NHibernate.Engine;

namespace Shoko.Server.Telemetry.Database;

/// <summary>
/// NHibernate's non-batching batcher, the one every backend uses today, reporting each statement to
/// <see cref="DatabaseTelemetry"/>.
/// </summary>
/// <remarks>
/// <c>ExecuteNonQuery</c> cannot be overridden, so this implements <see cref="IBatcher"/> again to take the calls NHibernate
/// makes through the interface. <c>AddToBatch</c> calls it directly, past the interface, and is overridden on its own.
/// </remarks>
internal sealed class TracingBatcher(ConnectionManager connectionManager, IInterceptor interceptor) : NonBatchingBatcher(connectionManager, interceptor), IBatcher
{
    public override void AddToBatch(IExpectation expectation)
    {
        var operation = DatabaseTelemetry.Start(CurrentCommand);
        try
        {
            base.AddToBatch(expectation);
            DatabaseTelemetry.Finish(operation);
        }
        catch (Exception failure)
        {
            DatabaseTelemetry.Finish(operation, failure);
            throw;
        }
    }

    public override async Task AddToBatchAsync(IExpectation expectation, CancellationToken cancellationToken)
    {
        var operation = DatabaseTelemetry.Start(CurrentCommand);
        try
        {
            await base.AddToBatchAsync(expectation, cancellationToken).ConfigureAwait(false);
            DatabaseTelemetry.Finish(operation);
        }
        catch (Exception failure)
        {
            DatabaseTelemetry.Finish(operation, failure);
            throw;
        }
    }

    public override DbDataReader ExecuteReader(DbCommand cmd)
    {
        var operation = DatabaseTelemetry.Start(cmd);
        try
        {
            var reader = base.ExecuteReader(cmd);
            DatabaseTelemetry.Finish(operation);
            return reader;
        }
        catch (Exception failure)
        {
            DatabaseTelemetry.Finish(operation, failure);
            throw;
        }
    }

    public override async Task<DbDataReader> ExecuteReaderAsync(DbCommand cmd, CancellationToken cancellationToken)
    {
        var operation = DatabaseTelemetry.Start(cmd);
        try
        {
            var reader = await base.ExecuteReaderAsync(cmd, cancellationToken).ConfigureAwait(false);
            DatabaseTelemetry.Finish(operation);
            return reader;
        }
        catch (Exception failure)
        {
            DatabaseTelemetry.Finish(operation, failure);
            throw;
        }
    }

    int IBatcher.ExecuteNonQuery(DbCommand cmd)
    {
        var operation = DatabaseTelemetry.Start(cmd);
        try
        {
            var rows = ExecuteNonQuery(cmd);
            DatabaseTelemetry.Finish(operation);
            return rows;
        }
        catch (Exception failure)
        {
            DatabaseTelemetry.Finish(operation, failure);
            throw;
        }
    }

    async Task<int> IBatcher.ExecuteNonQueryAsync(DbCommand cmd, CancellationToken cancellationToken)
    {
        var operation = DatabaseTelemetry.Start(cmd);
        try
        {
            var rows = await ExecuteNonQueryAsync(cmd, cancellationToken).ConfigureAwait(false);
            DatabaseTelemetry.Finish(operation);
            return rows;
        }
        catch (Exception failure)
        {
            DatabaseTelemetry.Finish(operation, failure);
            throw;
        }
    }
}
