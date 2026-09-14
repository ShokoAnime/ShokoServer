using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

    public JMMUser? AuthenticateUser(string userName, string? password)
    {
        password ??= string.Empty;
        var hashedPassword = Digest.Hash(password);
        var user = Cache.GetAll().FirstOrDefault(a => a.Username.Equals(userName, StringComparison.InvariantCultureIgnoreCase));
        if (user is null)
        {
            // Compare against a stand-in hash so unknown usernames don't answer noticeably faster
            // than existing ones, which would let the username list be enumerated by timing.
            PasswordMatches(_enumerationDefenseHash, hashedPassword);
            return null;
        }

        return PasswordMatches(user.Password, hashedPassword) ? user : null;
    }

    private static bool PasswordMatches(string? storedPassword, string hashedPassword)
    {
        if (string.IsNullOrEmpty(storedPassword))
            return string.IsNullOrEmpty(hashedPassword);

        var storedBytes = Encoding.UTF8.GetBytes(storedPassword);
        var hashedBytes = Encoding.UTF8.GetBytes(hashedPassword);
        return storedBytes.Length == hashedBytes.Length && CryptographicOperations.FixedTimeEquals(storedBytes, hashedBytes);
    }

    private const string _enumerationDefenseHash =
        "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000";
}
