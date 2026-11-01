using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// The server keeps its startup messages until a while after it has started.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class StartupMessageBacklogTests(DatabaseMigrationFixture fixture)
{
    [Fact]
    public void KeepsEveryMessageWhenStarted()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);

        Assert.True(fixture.StartupMessagesKeptOnStart > 1);
    }
}
