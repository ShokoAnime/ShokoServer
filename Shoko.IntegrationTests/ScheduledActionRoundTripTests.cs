using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.Models.Internal;
using Shoko.Server.Repositories.Cached;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Runs the step that fills the last scheduled run of the <c>ScheduledAction</c>
/// rows stored before it, on each backend.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ScheduledActionRoundTripTests(DatabaseMigrationFixture fixture)
{
    #region Helpers

    /// <summary>
    /// The backend's step filling the last scheduled run from the last run.
    /// </summary>
    /// <returns>The step's SQL.</returns>
    private string BackfillStep()
        => SchemaSteps.GetSql(fixture, 171);

    #endregion

    #region Tests

    [Fact]
    public void TheUpgradeStep_TakesEveryStoredLastRunAsAScheduledOne()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var repository = fixture.Services.GetRequiredService<ScheduledActionRepository>();
        var lastRunAt = new DateTime(2026, 9, 27, 4, 0, 0, DateTimeKind.Utc);
        var ran = new ScheduledAction { ActionID = Guid.NewGuid(), CreatedAt = lastRunAt.AddDays(-1), LastRunAt = lastRunAt };
        var neverRan = new ScheduledAction { ActionID = Guid.NewGuid(), CreatedAt = lastRunAt.AddDays(-1) };
        repository.Save(ran);
        repository.Save(neverRan);

        try
        {
            using (var connection = fixture.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = BackfillStep();
                command.ExecuteNonQuery();
            }

            repository.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(lastRunAt, DateTime.SpecifyKind(repository.GetByActionID(ran.ActionID)!.LastScheduledRunAt!.Value, DateTimeKind.Utc));
            Assert.Null(repository.GetByActionID(neverRan.ActionID)!.LastScheduledRunAt);
        }
        finally
        {
            repository.Delete(repository.GetByActionID(ran.ActionID)!);
            repository.Delete(repository.GetByActionID(neverRan.ActionID)!);
        }
    }

    #endregion
}
