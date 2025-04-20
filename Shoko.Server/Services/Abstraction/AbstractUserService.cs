using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Repositories.Cached;

#nullable enable
namespace Shoko.Server.Services.Abstraction;

public class AbstractUserService : IUserService
{
    private readonly JMMUserRepository _userRepository;

    public event EventHandler<UserChangedEventArgs>? UserAdded;

    public event EventHandler<UserChangedEventArgs>? UserUpdated;

    public event EventHandler<UserChangedEventArgs>? UserRemoved;

    public AbstractUserService(JMMUserRepository userRepository)
    {
        _userRepository = userRepository;
        _userRepository.UserAdded += OnUserAdded;
        _userRepository.UserUpdated += OnUserUpdated;
        _userRepository.UserRemoved += OnUserRemoved;
    }

    ~AbstractUserService()
    {
        _userRepository.UserAdded -= OnUserAdded;
        _userRepository.UserUpdated -= OnUserUpdated;
        _userRepository.UserRemoved -= OnUserRemoved;
    }

    private void OnUserAdded(object? sender, UserChangedEventArgs e)
    {
        UserAdded?.Invoke(this, e);
    }

    private void OnUserUpdated(object? sender, UserChangedEventArgs e)
    {
        UserUpdated?.Invoke(this, e);
    }

    private void OnUserRemoved(object? sender, UserChangedEventArgs e)
    {
        UserRemoved?.Invoke(this, e);
    }

    /// <inheritdoc/>
    public IEnumerable<IShokoUser> GetUsers()
         => _userRepository.GetAll();

    /// <inheritdoc/>
    public IShokoUser? GetUserByID(int id)
        => _userRepository.GetByID(id);
}
