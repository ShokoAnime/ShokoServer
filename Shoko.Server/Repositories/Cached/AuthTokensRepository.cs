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

        private AuthTokensRepository()
        {
        }

        public static AuthTokensRepository Create() => new AuthTokensRepository();

        public AuthTokens GetByToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            var tokens = Tokens.GetMultiple(token).ToList();
            var auth = tokens.FirstOrDefault();
            if (tokens.Count <= 1) return auth;
            tokens.Remove(auth);
            tokens.ForEach(Delete);
            return auth;
        }

        public void DeleteAllWithUserID(int id) => UserIDs.GetMultiple(id).ToList().ForEach(Delete);

        public void DeleteWithToken(string token)
        {
            if (!string.IsNullOrEmpty(token)) Tokens.GetMultiple(token).ToList().ForEach(Delete);
        }

        public List<AuthTokens> GetByUserID(int userID) => UserIDs.GetMultiple(userID).ToList();

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

            int uid = userrecord.JMMUserID;
            var tokens = UserIDs
                .GetMultiple(uid).ToList();
            var auth = tokens.FirstOrDefault(a =>
                a.DeviceName.Equals(device, StringComparison.InvariantCultureIgnoreCase) &&
                !string.IsNullOrEmpty(a.Token));
            if (tokens.Count > 1)
            {
                tokens.Remove(auth);
                tokens.ForEach(Delete);
            }
            var apiKey = auth?.Token ?? string.Empty;

            if (!string.IsNullOrEmpty(apiKey)) return apiKey;

            apiKey = Guid.NewGuid().ToString();
            AuthTokens token = new AuthTokens { UserID = uid, DeviceName = device.ToLowerInvariant(), Token = apiKey };
            RepoFactory.AuthTokens.Save(token);

            return apiKey;
        }
    }
}