using System.Diagnostics;
using NHibernate;

namespace JMMServer.Repositories.NHibernate
{
    internal static class SessionExtensions
    {
        [DebuggerStepThrough]
        public static ISessionWrapper Wrap(this ISession session)
        {
            return new SessionWrapper(session);
        }
    }
}