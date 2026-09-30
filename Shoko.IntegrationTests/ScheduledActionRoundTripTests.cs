using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Models.Internal;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Stores an action's triggers and last runs in the migrated database and
/// reads them back from it, so the <c>ScheduledAction</c> mapping, its column
/// types and its unique index are checked on each backend, and runs the step
/// that fills the last scheduled run of the rows stored before it.
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
    public void TriggersAndTheLastRunReadBackFromTheDatabase()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var repository = fixture.Services.GetRequiredService<ScheduledActionRepository>();
        var actionID = Guid.NewGuid();
        var lastRunAt = new DateTime(2026, 9, 28, 12, 30, 15, DateTimeKind.Utc);
        var triggers = ScheduledActionService.SerializeTriggers([ActionTrigger.AtStartup, ActionTrigger.WeeklyOn(DayOfWeek.Monday, new TimeOnly(9, 0))]);

        repository.Save(new ScheduledAction { ActionID = actionID, CreatedAt = lastRunAt.AddDays(-1), Triggers = triggers });
        repository.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);

        var row = repository.GetByActionID(actionID);
        Assert.NotNull(row);
        Assert.Equal(triggers, row.Triggers);
        Assert.Null(row.LastRunAt);
        Assert.Null(row.LastScheduledRunAt);

        // A later run updates the row rather than adding one, which the unique index would refuse.
        row.LastRunAt = lastRunAt;
        row.LastScheduledRunAt = lastRunAt.AddHours(-2);
        row.Triggers = null;
        repository.Save(row);
        repository.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);

        row = repository.GetByActionID(actionID);
        Assert.NotNull(row);
        Assert.Null(row.Triggers);
        Assert.Equal(lastRunAt, DateTime.SpecifyKind(row.LastRunAt!.Value, DateTimeKind.Utc));
        Assert.Equal(lastRunAt.AddHours(-2), DateTime.SpecifyKind(row.LastScheduledRunAt!.Value, DateTimeKind.Utc));

        repository.Delete(row);
    }

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
