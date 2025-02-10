using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Cached;

public class AuthTokensRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AuthTokens, int>(databaseFactory)
{
    private PocoIndex<int, AuthTokens, string> _tokens;

    private PocoIndex<int, AuthTokens, int> _userIDs;

    protected override int SelectKey(AuthTokens entity)
        => entity.AuthID;

    public override void PopulateIndexes()
    {
        _tokens = Cache.CreateIndex(a => a.Token);
        _userIDs = Cache.CreateIndex(a => a.UserID);
    }

    public AuthTokens GetByToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        var tokens = ReadLock(_tokens.GetMultiple(token.ToLowerInvariant().Trim()).ToList);
        var auth = tokens.FirstOrDefault();
        if (tokens.Count <= 1)
            return auth;

        tokens.Remove(auth);
        tokens.ForEach(Delete);
        return auth;
    }

    public void DeleteAllWithUserID(int id)
    {
        var ids = ReadLock(() => _userIDs.GetMultiple(id));
        ids.ForEach(Delete);
    }

    public void DeleteWithToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return;

        var tokens = ReadLock(() => _tokens.GetMultiple(token));
        tokens.ForEach(Delete);
    }

    public IReadOnlyList<AuthTokens> GetByUserID(int userID)
        => ReadLock(() => _userIDs.GetMultiple(userID));

    public string ValidateUser(string username, string password, string device)
        => CreateNewApiKey(RepoFactory.JMMUser.AuthenticateUser(username, password), device);

    public string CreateNewApiKey(JMMUser user, string device)
    {
        if (user == null)
            return string.Empty;

        // get tokens that are invalid
        var uid = user.JMMUserID;
        var ids = ReadLock(() => _userIDs.GetMultiple(uid));
        var tokens = ids.Where(a => !string.IsNullOrEmpty(a.Token) && a.DeviceName.Trim().Equals(device.Trim(), StringComparison.InvariantCultureIgnoreCase))
            .ToList();
        var auth = tokens.FirstOrDefault();
        if (tokens.Count > 1)
        {
            if (auth != null)
                tokens.Remove(auth);

            Delete(tokens);
        }

        var invalidTokens = ids
            .Where(a => string.IsNullOrEmpty(a.Token))
            .ToList();
        Delete(invalidTokens);

        var apiKey = auth?.Token.ToLowerInvariant().Trim() ?? string.Empty;

        if (!string.IsNullOrEmpty(apiKey))
            return apiKey;

        apiKey = Guid.NewGuid().ToString().ToLowerInvariant().Trim();
        var newToken = new AuthTokens { UserID = uid, DeviceName = device.Trim().ToLowerInvariant(), Token = apiKey };
        Save(newToken);

        return apiKey;
    }
}
