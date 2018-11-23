using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class AuthTokensRepository : BaseRepository<AuthTokens, int>
    {
        private PocoIndex<int, AuthTokens, string> Tokens;
        private PocoIndex<int, AuthTokens, int> UserIDs;

        internal override int SelectKey(AuthTokens entity) => entity.AuthID;

        internal override void PopulateIndexes()
        {
            Tokens = new PocoIndex<int, AuthTokens, string>(Cache, a => a.Token);
            UserIDs = new PocoIndex<int, AuthTokens, int>(Cache, a => a.UserID);
        }

        internal override void ClearIndexes()
        {
            Tokens = null;
            UserIDs = null;
        }


        public AuthTokens GetByToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            List<AuthTokens> tokens;
            using (RepoLock.ReaderLock())
            {
                 tokens = IsCached
                    ? Tokens.GetMultiple(token.ToLowerInvariant().Trim()).ToList()
                    : Table.Where(a => a.Token == token.ToLowerInvariant().Trim()).ToList();
            }
            AuthTokens auth = tokens.FirstOrDefault();
            if (tokens.Count <= 1) return auth;
            tokens.Remove(auth);
            this.Delete(tokens);
            return auth;
        }

        public void DeleteAllWithUserID(int userID)
        {
            List<AuthTokens> tokens;
            using (RepoLock.ReaderLock())
            {
                tokens = IsCached
                    ? UserIDs.GetMultiple(userID).ToList()
                    : Table.Where(a => a.UserID==userID).ToList();
            }
            this.Delete(tokens);
        }

        public void DeleteWithToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return;
            List<AuthTokens> tokens;
            using (RepoLock.ReaderLock())
            {
                tokens = IsCached
                    ? Tokens.GetMultiple(token.ToLowerInvariant().Trim()).ToList()
                    : Table.Where(a => a.Token == token.ToLowerInvariant().Trim()).ToList();
            }
            this.Delete(tokens);

        }

        public List<AuthTokens> GetByUserID(int userID)
        {
            using (RepoLock.ReaderLock())
            {
                return IsCached
                    ? UserIDs.GetMultiple(userID).ToList()
                    : Table.Where(a => a.UserID == userID).ToList();
            }
        }



        public string ValidateUser(string username, string password, string device)
        {
            JMMUser userrecord = Repo.Instance.JMMUser.AuthenticateUser(username, password);

            if (userrecord == null) return string.Empty;

            int uid = userrecord.JMMUserID;
            List<AuthTokens> tokens;
            using (RepoLock.ReaderLock())
            {
                tokens = IsCached
                    ? UserIDs.GetMultiple(uid).Where(a => string.IsNullOrEmpty(a.Token) || a.DeviceName.Trim().Equals(device.Trim(), StringComparison.InvariantCultureIgnoreCase)).ToList()
                    : Table.Where(a => a.UserID == uid && (string.IsNullOrEmpty(a.Token) || a.DeviceName.Trim().Equals(device.Trim(), StringComparison.InvariantCultureIgnoreCase))).ToList();
            }
            AuthTokens auth = tokens.FirstOrDefault(a => !string.IsNullOrEmpty(a.Token) && a.DeviceName.Trim().Equals(device.Trim(), StringComparison.InvariantCultureIgnoreCase));
            if (tokens.Count > 1)
            {
                if (auth != null) tokens.Remove(auth);
                this.Delete(tokens);
            }
            string apiKey = auth?.Token.ToLowerInvariant().Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(apiKey)) return apiKey;

            apiKey = Guid.NewGuid().ToString().ToLowerInvariant().Trim();
            using (var obj = BeginAdd())
            {
                obj.Entity.UserID = uid;
                obj.Entity.DeviceName = device.Trim().ToLowerInvariant();
                obj.Entity.Token = apiKey;
                obj.Commit();
            }
            return apiKey;
        }
    }
}