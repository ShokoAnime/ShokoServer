using NHibernate;

namespace JMMServer.Repositories.NHibernate
{
    internal static class SessionExtensions
    {
        public static ISessionWrapper Wrap(this ISession session)
        {
            return new SessionWrapper(session);
        }
    }
}