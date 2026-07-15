using System;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Repositories.Cached;

public class JMMUserRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<JMMUser, int>(databaseFactory)
{
    protected override int SelectKey(JMMUser entity)
        => entity.JMMUserID;

    public JMMUser? GetByUsername(string? username)
        => !string.IsNullOrWhiteSpace(username)
            ? Cache.GetAll().FirstOrDefault(user => string.Equals(user.Username, username, StringComparison.InvariantCultureIgnoreCase))
            : null;

    public JMMUser? GetAniDBUser()
        => Cache.GetAll().FirstOrDefault(a => a.IsAniDBUser == 1);

    public JMMUser? GetByExternalAuthID(string? externalAuthID)
        => !string.IsNullOrWhiteSpace(externalAuthID)
            ? Cache.GetAll().FirstOrDefault(user => string.Equals(user.ExternalAuthID, externalAuthID, StringComparison.Ordinal))
            : null;

    public JMMUser? AuthenticateUser(string userName, string? password)
    {
        password ??= string.Empty;
        var hashedPassword = Digest.Hash(password);
        return Cache.GetAll().FirstOrDefault(a =>
            a.Username.Equals(userName, StringComparison.InvariantCultureIgnoreCase) &&
            a.Password.Equals(hashedPassword)
        );
    }
}
