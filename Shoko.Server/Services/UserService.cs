using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.User;
using Shoko.Server.API;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;

#nullable enable
namespace Shoko.Server.Services;

public class UserService(
    ILogger<UserService> _logger,
    ISchedulerFactory _schedulerFactory,
    JMMUserRepository _userRepository,
    AuthTokensRepository _authTokensRepository,
    AnimeSeriesRepository _seriesRepository,
    AnimeGroup_UserRepository _groupUserRepository,
    AnimeSeries_UserRepository _seriesUserRepository,
    AnimeEpisode_UserRepository _episodeUserRepository,
    VideoLocal_UserRepository _videoUserRepository
) : IUserService
{
    public event EventHandler<UserChangedEventArgs>? UserAdded;

    public event EventHandler<UserChangedEventArgs>? UserUpdated;

    public event EventHandler<UserChangedEventArgs>? UserRemoved;

    /// <inheritdoc/>
    public IEnumerable<IUser> GetUsers()
         => _userRepository.GetAll();

    /// <inheritdoc/>
    public IUser? GetUserByID(int id)
        => id is <= 0 ? null : _userRepository.GetByID(id);

    public IUser? GetUserByUsername(string name)
        => string.IsNullOrEmpty(name) ? null : _userRepository.GetByUsername(name);

    public IUser? GetUserFromHttpContext(HttpContext context)
        => context.GetUser();

    public Task<IUser> CreateUser(UserUpdateData initialData)
        => UpdateUserInternal(new JMMUser(), initialData);

    public Task ResetUserPassword(IUser user)
        => UpdateUser(user, new() { Password = string.Empty });

    public Task ChangeUserPassword(IUser user, string newPassword)
        => UpdateUser(user, new() { Password = newPassword });

    public Task<IUser> UpdateUser(IUser user, UserUpdateData updateData)
        => UpdateUserInternal((JMMUser)user, updateData);

    private async Task<IUser> UpdateUserInternal(JMMUser user, UserUpdateData updateData)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(updateData);

        var errorDict = new Dictionary<string, List<string>>();
        void AddModelError(string key, string message)
        {
            if (!errorDict.ContainsKey(key)) errorDict.Add(key, []);
            errorDict[key].Add(message);
        }

        if (!string.IsNullOrEmpty(updateData.Username))
            user.Username = updateData.Username.Trim();
        if ((user.JMMUserID is 0 || string.IsNullOrEmpty(user.Username)) && string.IsNullOrWhiteSpace(updateData.Username))
            AddModelError(nameof(updateData.Username), "A new user must have a username set.");
        if (updateData.Username is not null && string.IsNullOrWhiteSpace(updateData.Username))
            AddModelError(nameof(updateData.Username), "Username cannot be empty or only white-spaces.");

        if (updateData.Password is { Length: > 1024 })
            AddModelError(nameof(updateData.Password), "Password cannot be longer than 1024 characters. Why the fuck do you need more than 1024 characters in your password?");
        if (user.JMMUserID is 0 && user.Password is null)
            AddModelError(nameof(updateData.Username), "A new user must have a password set.");

        if (_userRepository.GetByUsername(updateData.Username) is { } existingUser && existingUser.JMMUserID != user.JMMUserID)
            AddModelError(nameof(updateData.Username), "The username is unavailable.");

        if (updateData.IsAdmin.HasValue && user.IsAdminUser() != updateData.IsAdmin.Value)
        {
            var allAdmins = _userRepository.GetAll().Where(a => a.IsAdminUser()).ToList();
            allAdmins.Remove(user);
            if (allAdmins.Count < 1)
                AddModelError(nameof(updateData.IsAdmin), "There must be at least one admin user.");
        }

        var isNew = user.JMMUserID is 0;
        var shouldSave = isNew;
        var updateStats = isNew;

        // Try to update the avatar for the user. It will add model errors if it fails.
        if (updateData.HasSetAvatarImage)
            if (updateData.AvatarImageAsStream is not null)
                shouldSave = user.SetAvatarImage(updateData.AvatarImageAsStream, "AvatarImage", AddModelError) || shouldSave;
            else
                shouldSave = user.SetAvatarImage(updateData.AvatarImage, "AvatarImage", AddModelError) || shouldSave;

        // Return early if the model state was invalidated.
        if (errorDict.Count > 0)
            throw new GenericValidationException(
                "Got validation errors while attempting to save user.",
                errorDict.Select(a => KeyValuePair.Create(a.Key, (IReadOnlyList<string>)a.Value)).ToDictionary()
            );

        if (!string.IsNullOrEmpty(updateData.Username) && user.Username != updateData.Username)
        {
            shouldSave = true;
            user.Username = updateData.Username;
        }

        if (updateData.IsAdmin.HasValue && (user.IsAdminUser() != updateData.IsAdmin.Value || user.CanEditServerSettings == 1 != updateData.IsAdmin.Value))
        {
            shouldSave = true;
            user.IsAdmin = updateData.IsAdmin.Value ? 1 : 0;
            user.CanEditServerSettings = updateData.IsAdmin.Value ? 1 : 0;
        }

        if (updateData.IsAnidbUser.HasValue)
        {
            if (updateData.IsAnidbUser.Value && user.IsAniDBUser != 1)
            {
                if (_userRepository.GetAll().FirstOrDefault(u => u.IsAniDBUser == 1) is { } anidbUser)
                {
                    anidbUser.IsAniDBUser = 0;
                    _userRepository.Save(anidbUser);
                    try
                    {
                        UserUpdated?.Invoke(this, new() { User = anidbUser });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "An error occurred while trying to send the UserUpdated event; {Message}", ex.Message);
                    }
                }

                shouldSave = true;
                updateStats = true;
                user.IsAniDBUser = 1;
            }
            else if (!updateData.IsAnidbUser.Value && user.IsAniDBUser == 1)
            {
                shouldSave = true;
                user.IsAniDBUser = 0;
            }
        }

        if (updateData.RestrictedTags is not null)
        {
            var tags = updateData.RestrictedTags
                .OrderBy(a => a.ID)
                .Select(tag => tag.Name)
                .WhereNotNull()
                .Distinct(StringComparer.InvariantCultureIgnoreCase)
                .Join(',');
            if (!string.Equals(user.HideCategories, tags, StringComparison.InvariantCultureIgnoreCase))
            {
                shouldSave = true;
                user.HideCategories = tags;
            }
        }

        if (updateData.Password is not null)
        {
            var hash = string.IsNullOrEmpty(updateData.Password) ? string.Empty : Digest.Hash(updateData.Password);
            if (!string.Equals(user.Password, hash, StringComparison.Ordinal))
            {
                shouldSave = true;
                user.Password = hash;
            }
        }

        if (shouldSave)
        {
            _userRepository.Save(user);

            try
            {
                if (isNew)
                    UserAdded?.Invoke(this, new() { User = user });
                else
                    UserUpdated?.Invoke(this, new() { User = user });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while trying to send the UserUpdated event; {Message}", ex.Message);
            }

            if (updateStats)
            {
                var scheduler = await _schedulerFactory.GetScheduler();
                await Task.WhenAll(_seriesRepository.GetAll().Select(ser => scheduler.StartJob<RefreshAnimeStatsJob>(a => a.AnimeID = ser.AniDB_ID)));
            }
        }

        return user;
    }

    public Task DeleteUser(IUser user)
    {
        if (_userRepository.GetByID(user.ID) is { } nativeUser)
        {
            var allAdmins = _userRepository.GetAll().Where(a => a.IsAdmin == 1).ToList();
            allAdmins.Remove(nativeUser);
            if (allAdmins.Count < 1)
                return Task.FromException(new GenericValidationException(
                    "Got validation errors while attempting to delete user.",
                    new Dictionary<string, IReadOnlyList<string>> { { "IsAdmin", ["There must be at least one admin user."] } }
                ));
            _userRepository.Delete(nativeUser);

            try
            {
                UserRemoved?.Invoke(this, new() { User = nativeUser });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while trying to send the UserRemoved event; {Message}", ex.Message);
            }
        }

        _groupUserRepository.Delete(_groupUserRepository.GetByUserID(user.ID));
        _seriesUserRepository.Delete(_seriesUserRepository.GetByUserID(user.ID));
        _episodeUserRepository.Delete(_episodeUserRepository.GetByUserID(user.ID));
        _videoUserRepository.Delete(_videoUserRepository.GetByUserID(user.ID));

        return Task.CompletedTask;
    }

    public IUser? AuthenticateUser(string username, string password)
        => string.IsNullOrEmpty(username) ? null : _userRepository.AuthenticateUser(username, password);

    public (string? Token, string? DeviceName) GetRestApiTokenFromHttpContext(HttpContext context)
        => context.GetToken();

    public Task<string> GenerateRestApiTokenForUser(IUser user, string deviceName)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.ID is <= 0 || _userRepository.GetByID(user.ID) is not { } otherUser)
            throw new ArgumentException("User is not stored in the database!", nameof(user));

        var token = _authTokensRepository.CreateNewApiKey(otherUser, deviceName);
        return Task.FromResult(token);
    }

    public IReadOnlyList<string> ListRestApiDevicesForUser(IUser user)
        => _authTokensRepository.GetByUserID(user.ID)
            .Select(a => a.DeviceName)
            .ToList();

    public Task<bool> InvalidateRestApiDeviceForUser(IUser user, string deviceName)
    {
        var tokens = _authTokensRepository.GetByUserID(user.ID)
            .Where(a => a.DeviceName.Equals(deviceName, StringComparison.InvariantCultureIgnoreCase))
            .ToList();
        foreach (var token in tokens)
            _authTokensRepository.Delete(token);
        return Task.FromResult(tokens.Count > 0);
    }

    public Task<bool> InvalidateRestApiTokensForUser(IUser user)
        => Task.FromResult(_authTokensRepository.DeleteAllWithUserID(user.ID));

    public Task<bool> InvalidateRestApiToken(string token)
        => Task.FromResult(_authTokensRepository.DeleteWithToken(token));
}
