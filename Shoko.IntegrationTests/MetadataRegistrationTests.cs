using System;
using Shoko.Abstractions.Metadata;
using Xunit;

namespace Shoko.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class MetadataRegistrationTests(DatabaseMigrationFixture fixture)
{
    [Fact]
    public void RegistrationIsClosedOnceTheServerHasStarted()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);

        Assert.Throws<InvalidOperationException>(() => MetadataSource.Register("IntegrationLate", "integration-late"));
        Assert.Throws<InvalidOperationException>(() => MetadataEntityType.Register("IntegrationLate", "integration-late"));
    }
}
