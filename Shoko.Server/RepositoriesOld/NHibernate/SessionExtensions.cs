using System.Diagnostics;
using NHibernate;

namespace Shoko.Server.Repositories.NHibernate
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