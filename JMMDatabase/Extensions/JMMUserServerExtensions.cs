using System.Collections.Generic;
using System.Linq;
using JMMModels;
using JMMModels.Childs;
using Raven.Client;

namespace JMMDatabase.Extensions
{
    public static class JMMUserServerExtensions
    {
        public static JMMUser GetAniDBUser(this JMMUser user)
        {
            AniDBAuthorization a = GetAniDBAuthorizationFromUser(user);
            if (a != null)
                return user;
            while (!string.IsNullOrEmpty(user.ParentId))
            {
                user = Store.JmmUserRepo.Find(user.ParentId);
                a = GetAniDBAuthorizationFromUser(user);
                if (a != null)
                    return user;
            }
            return null;
        }

        public static JMMUser GetRealUser(this JMMUser user)
        {
            if (user.IsRealUserAccount)
                return user;
            return user.GetAniDBUser();
        }
        public static AniDBAuthorization GetAniDBAuthorizationFromUser(this JMMUser user)
        {
            foreach (object auth in user.Authorizations)
            {
                AniDBAuthorization a = auth as AniDBAuthorization;
                if (a != null)
                    return a;
            }
            return null;
        }

        public static bool HasAniDBAccount(this JMMUser user)
        {
            return GetAniDBAuthorizationFromUser(user) != null;
        }
        
    }
}
