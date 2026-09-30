using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.User.Enums;
using Shoko.Abstractions.User.Events;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   The user events say what changed on the user.
/// </summary>
public class UserServiceUserEventTests
{
    [Fact]
    public async Task ChangePassword_IsAPasswordChange()
    {
        var (harness, updated) = Create();

        await harness.Service.ChangeUserPassword(harness.Users[0], "hunter2");

        Assert.Equal(UserSaveReason.Password, Assert.Single(updated).Reason);
    }

    [Fact]
    public async Task Update_NamesEveryChangedField()
    {
        var (harness, updated) = Create();

        await harness.Service.UpdateUser(harness.Users[1], new() { Username = "robert", IsAdmin = false });

        Assert.Equal(UserSaveReason.Username | UserSaveReason.IsAdmin, Assert.Single(updated).Reason);
    }

    [Fact]
    public async Task Update_WithNothingChanged_RaisesNothing()
    {
        var (harness, updated) = Create();

        await harness.Service.UpdateUser(harness.Users[0], new() { Username = "alice" });

        Assert.Empty(updated);
    }

    [Fact]
    public async Task Delete_RaisesRemovedOnce()
    {
        var harness = new UserServiceApiTokenEventTests.Harness();
        var removed = new List<UserChangedEventArgs>();
        harness.Service.UserRemoved += (_, e) => removed.Add(e);

        await harness.Service.DeleteUser(harness.Users[0]);

        Assert.Same(harness.Users[0], Assert.Single(removed).User);
    }

    private static (UserServiceApiTokenEventTests.Harness Harness, List<UserChangedEventArgs> Updated) Create()
    {
        var harness = new UserServiceApiTokenEventTests.Harness();
        var updated = new List<UserChangedEventArgs>();
        harness.Service.UserUpdated += (_, e) => updated.Add(e);
        return (harness, updated);
    }
}
