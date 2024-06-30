using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Cached;

public class AuthTokensRepository : BaseCachedRepository<AuthTokens, int>
{
    private PocoIndex<int, AuthTokens, string> Tokens;
    private PocoIndex<int, AuthTokens, int> UserIDs;

    public AuthTokens GetByToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var tokens = ReadLock(Tokens.GetMultiple(token.ToLowerInvariant().Trim()).ToList);
        var auth = tokens.FirstOrDefault();
        if (tokens.Count <= 1)
        {
            return auth;
        }

        tokens.Remove(auth);
        tokens.ForEach(Delete);
        return auth;
    }

    public void DeleteAllWithUserID(int id)
    {
        var ids = ReadLock(() => UserIDs.GetMultiple(id));
        ids.ForEach(Delete);
    }

    public void DeleteWithToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        var tokens = ReadLock(() => Tokens.GetMultiple(token));
        tokens.ForEach(Delete);
    }

    public List<AuthTokens> GetByUserID(int userID)
    {
        return ReadLock(() => UserIDs.GetMultiple(userID));
    }

    protected override int SelectKey(AuthTokens entity)
    {
        return entity.AuthID;
    }

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
        JMMUser user = RepoFactory.JMMUser.AuthenticateUser(username, password);
        return CreateNewApikey(user, device);
    }

    public string CreateNewApikey(JMMUser user, string device)
    {
        if (user == null) return string.Empty;

        // get tokens that are invalid
        var uid = user.JMMUserID;
        var ids = ReadLock(() => UserIDs.GetMultiple(uid));
        var tokens = ids.Where(a => !string.IsNullOrEmpty(a.Token) && a.DeviceName.Trim().Equals(device.Trim(), StringComparison.InvariantCultureIgnoreCase))
            .ToList();
        var auth = tokens.FirstOrDefault();
        if (tokens.Count > 1)
        {
            if (auth != null) tokens.Remove(auth);

            Delete(tokens);
        }

        var invalidTokens = ids.Where(a => string.IsNullOrEmpty(a.Token)).ToList();
        Delete(invalidTokens);

        var apiKey = auth?.Token.ToLowerInvariant().Trim() ?? string.Empty;

        if (!string.IsNullOrEmpty(apiKey)) return apiKey;

        apiKey = Guid.NewGuid().ToString().ToLowerInvariant().Trim();
        var newToken = new AuthTokens { UserID = uid, DeviceName = device.Trim().ToLowerInvariant(), Token = apiKey };
        Save(newToken);

        return apiKey;
    }


    public AuthTokensRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
