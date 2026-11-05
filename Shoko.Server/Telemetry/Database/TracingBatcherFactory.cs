using NHibernate;
using NHibernate.AdoNet;
using NHibernate.Engine;

namespace Shoko.Server.Telemetry.Database;

/// <summary>
/// Creates the <see cref="TracingBatcher"/> of a session.
/// </summary>
internal sealed class TracingBatcherFactory : IBatcherFactory
{
    public IBatcher CreateBatcher(ConnectionManager connectionManager, IInterceptor interceptor)
        => new TracingBatcher(connectionManager, interceptor);
}
