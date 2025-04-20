using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Server.Databases;
using Shoko.Server.Models;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class JMMUserRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<SVR_JMMUser, int>(databaseFactory)
{
    public event EventHandler<UserChangedEventArgs>? UserAdded;

    public event EventHandler<UserChangedEventArgs>? UserUpdated;

    public event EventHandler<UserChangedEventArgs>? UserRemoved;

    protected override int SelectKey(SVR_JMMUser entity)
        => entity.JMMUserID;

    public override void Save(SVR_JMMUser user)
    {
        var isNew = user.JMMUserID <= 0;

        base.Save(user);

        if (isNew)
            Task.Run(() => UserAdded?.Invoke(null, new() { User = user }));
        else
            Task.Run(() => UserUpdated?.Invoke(null, new() { User = user }));
    }

    public SVR_JMMUser? GetByUsername(string? username)
        => !string.IsNullOrWhiteSpace(username)
            ? ReadLock(() => Cache.Values.FirstOrDefault(user => string.Equals(user.Username, username, StringComparison.InvariantCultureIgnoreCase)))
            : null;

    public IReadOnlyList<SVR_JMMUser> GetAniDBUsers()
        => ReadLock(() => Cache.Values.Where(a => a.IsAniDBUser == 1).ToList());

    public IReadOnlyList<SVR_JMMUser> GetTraktUsers()
        => ReadLock(() => Cache.Values.Where(a => a.IsTraktUser == 1).ToList());

    public SVR_JMMUser? AuthenticateUser(string userName, string? password)
    {
        password ??= string.Empty;
        var hashedPassword = Digest.Hash(password);
        return ReadLock(() => Cache.Values.FirstOrDefault(a =>
            a.Username.Equals(userName, StringComparison.InvariantCultureIgnoreCase) &&
            a.Password.Equals(hashedPassword)
        ));
    }

    public bool RemoveUser(int userID, bool skipValidation = false)
    {
        var user = GetByID(userID);
        if (!skipValidation)
        {
            var allAdmins = GetAll().Where(a => a.IsAdmin == 1).ToList();
            allAdmins.Remove(user);
            if (allAdmins.Count < 1)
            {
                return false;
            }
        }

        RepoFactory.AnimeSeries_User.Delete(RepoFactory.AnimeSeries_User.GetByUserID(userID));
        RepoFactory.AnimeGroup_User.Delete(RepoFactory.AnimeGroup_User.GetByUserID(userID));
        RepoFactory.AnimeEpisode_User.Delete(RepoFactory.AnimeEpisode_User.GetByUserID(userID));
        RepoFactory.VideoLocalUser.Delete(RepoFactory.VideoLocalUser.GetByUserID(userID));

        Delete(user);
        return true;
    }

    public override void Delete(SVR_JMMUser user)
    {
        base.Delete(user);

        Task.Run(() => UserRemoved?.Invoke(null, new() { User = user }));
    }
}
