using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class JMMUserRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<JMMUser, int>(databaseFactory)
{
    protected override int SelectKey(JMMUser entity)
        => entity.JMMUserID;

    public new void Save(JMMUser user)
    {
        base.Save(user);
    }

    public JMMUser? GetByUsername(string? username)
        => !string.IsNullOrWhiteSpace(username)
            ? ReadLock(() => Cache.Values.FirstOrDefault(user => string.Equals(user.Username, username, StringComparison.InvariantCultureIgnoreCase)))
            : null;

    public JMMUser? GetAniDBUser()
        => ReadLock(() => Cache.Values.Where(a => a.IsAniDBUser == 1).FirstOrDefault());

    public IReadOnlyList<JMMUser> GetTraktUsers()
        => ReadLock(() => Cache.Values.Where(a => a.IsTraktUser == 1).ToList());

    public JMMUser? AuthenticateUser(string userName, string? password)
    {
        password ??= string.Empty;
        var hashedPassword = Digest.Hash(password);
        return ReadLock(() => Cache.Values.FirstOrDefault(a =>
            a.Username.Equals(userName, StringComparison.InvariantCultureIgnoreCase) &&
            a.Password.Equals(hashedPassword)
        ));
    }
}
