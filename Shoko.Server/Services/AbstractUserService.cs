using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Repositories.Cached;

#nullable enable
namespace Shoko.Server.Services;

public class AbstractUserService : IUserService
{
    private readonly JMMUserRepository _userRepository;

    public AbstractUserService(JMMUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <inheritdoc/>
    public IEnumerable<IShokoUser> GetUsers()
         => _userRepository.GetAll();

    /// <inheritdoc/>
    public IShokoUser? GetUserByID(int id)
        => _userRepository.GetByID(id);
}
