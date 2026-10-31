using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.Internal;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Utilities;

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

        // An expired token is unknown from the moment it expires. It is removed, and its
        // removal announced, by the expired token cleanup (`UserService.InvalidateExpiredApiTokens`),
        // not here on the authentication path.
        if (auth is { ExpiresAt: not null } && auth.ExpiresAt.Value < DateTime.Now)
            return null;

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

    public IReadOnlyList<AuthTokens> GetAllByToken(string token)
        => _tokens!.GetMultiple(token);

    public IReadOnlyList<AuthTokens> GetByUserID(int userID)
        => _userIDs!.GetMultiple(userID);

    public AuthTokens CreateNewApiKey(JMMUser user, string device)
        => CreateNewApiKey(user, device, out _, out _);

    /// <summary>
    ///   Hands back the user's permanent token for the device, or creates one,
    ///   removing any other permanent tokens of the device and tokens with no
    ///   key.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="device">The device name, matched ignoring case.</param>
    /// <param name="created">Whether a new token was created.</param>
    /// <param name="removedDuplicates">The other permanent tokens of the device that were removed.</param>
    /// <returns>The token.</returns>
    public AuthTokens CreateNewApiKey(JMMUser user, string device, out bool created, out IReadOnlyList<AuthTokens> removedDuplicates)
    {
        created = false;
        var allTokensForUser = _userIDs!.GetMultiple(user.JMMUserID);
        var existingTokens = allTokensForUser
            .Where(a => !a.ExpiresAt.HasValue && !string.IsNullOrEmpty(a.Token) && a.DeviceName.Trim().Equals(device.Trim(), StringComparison.InvariantCultureIgnoreCase))
            .ToList();
        var invalidTokens = allTokensForUser
            .Where(a => string.IsNullOrEmpty(a.Token))
            .ToList();
        removedDuplicates = existingTokens.Skip(1).ToList();
        invalidTokens.AddRange(removedDuplicates);

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
        created = true;

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
