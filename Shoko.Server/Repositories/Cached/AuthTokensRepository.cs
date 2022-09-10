using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Cached
{
    public class AuthTokensRepository : BaseCachedRepository<AuthTokens, int>
    {
        private PocoIndex<int, AuthTokens, string> Tokens;
        private PocoIndex<int, AuthTokens, int> UserIDs;

        public AuthTokens GetByToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;

            Lock.EnterReadLock();
            var tokens = Tokens.GetMultiple(token.ToLowerInvariant().Trim()).ToList();
            Lock.ExitReadLock();
            var auth = tokens.FirstOrDefault();
            if (tokens.Count <= 1) return auth;
            tokens.Remove(auth);
            tokens.ForEach(Delete);
            return auth;
        }

        public void DeleteAllWithUserID(int id)
        {
            Lock.EnterReadLock();
            var ids = UserIDs.GetMultiple(id);
            Lock.ExitReadLock();
            ids.ForEach(Delete);
        }

        public void DeleteWithToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return;
            Lock.EnterReadLock();
            var tokens = Tokens.GetMultiple(token);
            Lock.ExitReadLock();
            tokens.ForEach(Delete);
        }

        public List<AuthTokens> GetByUserID(int userID)
        {
            Lock.EnterReadLock();
            var result = UserIDs.GetMultiple(userID);
            Lock.ExitReadLock();
            return result;
        }

        protected override int SelectKey(AuthTokens entity) => entity.AuthID;

        public override void PopulateIndexes()
        {
            Tokens = new PocoIndex<int, AuthTokens, string>(Cache, a => a.Token);
            UserIDs = new PocoIndex<int, AuthTokens, int>(Cache, a => a.UserID);
        }

        public override void RegenerateDb()
        {
        }

        public string ValidateUser(string username, string password, string device)
        {
            JMMUser userrecord = RepoFactory.JMMUser.AuthenticateUser(username, password);

            if (userrecord == null) return string.Empty;

            var uid = userrecord.JMMUserID;
            Lock.EnterReadLock();
            var ids = UserIDs.GetMultiple(uid);
            Lock.ExitReadLock();
            var tokens = ids.Where(a => string.IsNullOrEmpty(a.Token) ||
                                             a.DeviceName.Trim().Equals(device.Trim(),
                                                 StringComparison.InvariantCultureIgnoreCase))
                .ToList();
            var auth = tokens.FirstOrDefault(a => !string.IsNullOrEmpty(a.Token) &&
                                                  a.DeviceName.Trim().Equals(device.Trim(),
                                                      StringComparison.InvariantCultureIgnoreCase));
            if (tokens.Count > 1)
            {
                if (auth != null) tokens.Remove(auth);
                tokens.ForEach(Delete);
            }
            var apiKey = auth?.Token.ToLowerInvariant().Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(apiKey)) return apiKey;

            apiKey = Guid.NewGuid().ToString().ToLowerInvariant().Trim();
            var newToken = new AuthTokens {UserID = uid, DeviceName = device.Trim().ToLowerInvariant(), Token = apiKey};
            Save(newToken);

            return apiKey;
        }
    }
}
