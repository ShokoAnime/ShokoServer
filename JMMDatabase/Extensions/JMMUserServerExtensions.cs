using System.Collections.Generic;
using System.Linq;
using JMMModels;
using JMMModels.Childs;
using Raven.Client;

namespace JMMDatabase.Extensions
{
    public static class JMMUserServerExtensions
    {
        public static JMMUser GetUserWithAuth(this JMMUser user, AuthorizationProvider provider)
        {
            while (user!=null)
            {
                if (user.IsRealUserAccount)
                {
                    BaseAuthorization b = user.GetAuthorization<BaseAuthorization>(provider);
                    if (b != null)
                        return user;
                }
                if (string.IsNullOrEmpty(user.ParentId))
                    return null;
                user = Store.JmmUserRepo.Find(user.ParentId);
            }
            return null;
        }

        public static T GetAuthorization<T>(this JMMUser user, AuthorizationProvider p) where T : BaseAuthorization
        {
            foreach (object auth in user.Authorizations)
            {
                T b = auth as T;
                if ((b != null) && (b.Provider == p))
                    return b;
            }
            return null;
        }

        public static JMMUser GetRealUser(this JMMUser user)
        {
            while (!user.IsRealUserAccount)
            {
                if (!string.IsNullOrEmpty(user.ParentId))
                {
                    user = Store.JmmUserRepo.Find(user.ParentId);
                    if (user == null)
                        return null;
                }
                else
                    return null;
            }
            return user;
        }


        public static AniDBAuthorization GetAniDBAuthorization(this JMMUser user)
        {
            return user.GetAuthorization<AniDBAuthorization>(AuthorizationProvider.AniDB);
        }

        public static UserNameAuthorization GetMALAuthorization(this JMMUser user)
        {
            return user.GetAuthorization<UserNameAuthorization>(AuthorizationProvider.MAL);
        }

        public static TraktAuthorization GetTraktAuthorization(this JMMUser user)
        {
            return user.GetAuthorization<TraktAuthorization>(AuthorizationProvider.Trakt);
        }
        public static bool HasAniDBAccount(this JMMUser user)
        {
            return GetAniDBAuthorization(user) != null;
        }
        public static bool HasMALAccount(this JMMUser user)
        {
            return GetMALAuthorization(user) != null;
        }
        public static bool HasTraktAccount(this JMMUser user)
        {
            return GetTraktAuthorization(user) != null;
        }

    }
}
