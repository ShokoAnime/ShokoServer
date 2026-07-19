using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.Internal;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Repositories.Cached;

public class AuthTokensRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AuthTokens, int>(databaseFactory)
{
    private PocoIndex<int, AuthTokens, string>? _tokens;

    private PocoIndex<int, AuthTokens, int>? _userIDs;

    protected override int SelectKey(AuthTokens entity)
        => entity.AuthID;

    public override void PopulateIndexes()
    {
        _tokens = Cache.CreateIndex(a => a.Token);
        _userIDs = Cache.CreateIndex(a => a.UserID);
    }

    public AuthTokens? GetByToken(string? token)
    {
        if (string.IsNullOrEmpty(token) || !Guid.TryParse(token, out var guid))
            return null;

        var tokens = _tokens!.GetMultiple(guid.ToString("D").ToLowerInvariant().Trim()).ToList();
        var auth = tokens.FirstOrDefault();
        if (tokens.Count > 1)
        {
            tokens.Remove(auth!);
            Delete(tokens);
        }

        // Lazy-invalidate the token upon read if it hasn't been cleaned up yet.
        if (auth is { ExpiresAt: not null } && auth.ExpiresAt.Value < DateTime.Now)
        {
            Delete(auth);
            return null;
        }

        return auth;
    }

    public bool DeleteAllWithUserID(int id)
    {
        var ids = _userIDs!.GetMultiple(id);
        ids.ForEach(Delete);
        return ids.Count > 0;
    }

    public bool DeleteWithToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        var tokens = _tokens!.GetMultiple(token);
        Delete(tokens);
        return tokens.Count > 0;
    }

    public IReadOnlyList<AuthTokens> GetByUserID(int userID)
        => _userIDs!.GetMultiple(userID);

    public bool DeleteWithUserIDAndDevicePrefix(int userID, string devicePrefix)
    {
        var tokens = _userIDs!.GetMultiple(userID).Where(a => a.DeviceName.StartsWith(devicePrefix, StringComparison.Ordinal)).ToList();
        Delete(tokens);
        return tokens.Count > 0;
    }

    public AuthTokens CreateNewApiKey(JMMUser user, string device)
    {
        var allTokensForUser = _userIDs!.GetMultiple(user.JMMUserID);
        var existingTokens = allTokensForUser
            .Where(a => !a.ExpiresAt.HasValue && !string.IsNullOrEmpty(a.Token) && a.DeviceName.Trim().Equals(device.Trim(), StringComparison.InvariantCultureIgnoreCase))
            .ToList();
        var invalidTokens = allTokensForUser
            .Where(a => string.IsNullOrEmpty(a.Token))
            .ToList();
        if (existingTokens.Count > 1)
            invalidTokens.AddRange(existingTokens.Skip(1));

        Delete(invalidTokens);

        var validToken = existingTokens.FirstOrDefault();
        if (!string.IsNullOrEmpty(validToken?.Token.ToLowerInvariant().Trim()))
            return validToken!;

        var token = new AuthTokens
        {
            UserID = user.JMMUserID,
            DeviceName = device.Trim(),
            Token = Guid.NewGuid().ToString().ToLowerInvariant().Trim(),
        };

        Save(token);

        return token;
    }

    public AuthTokens CreateExpiringApiKey(JMMUser user, string device, DateTime expiresAt)
    {
        var token = new AuthTokens
        {
            UserID = user.JMMUserID,
            DeviceName = device.Trim(),
            Token = Guid.NewGuid().ToString().ToLowerInvariant().Trim(),
            ExpiresAt = expiresAt,
        };

        Save(token);

        return token;
    }
}
