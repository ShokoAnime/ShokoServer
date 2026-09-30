using System;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Server.Databases;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// When the start-up compacts an SQLite database: only after an upgrade that ran schema steps,
/// only when its free pages hold more than a tenth of it or more than 100 MB, and only when its
/// drive has twice its size free.
/// </summary>
public class StartupCompactionTests
{
    #region Constants

    private const long MB = 1024 * 1024;

    private const long GB = 1024 * MB;

    #endregion

    #region Schema Steps

    [Fact]
    public void ANormalStartDoesNotTouchTheDatabase()
    {
        var database = new Mock<IDatabase>(MockBehavior.Strict);

        var decision = StartupCompaction.Run(database.Object, false, NullLogger.Instance, _ => throw new InvalidOperationException());

        Assert.Equal(StartupCompactionDecision.NoUpgrade, decision);
        database.VerifyNoOtherCalls();
    }

    [Fact]
    public void AnUpgradeOnAnotherBackendDoesNotTouchTheDatabase()
    {
        var database = new Mock<IDatabase>(MockBehavior.Strict);

        var decision = StartupCompaction.Run(database.Object, true, NullLogger.Instance, _ => throw new InvalidOperationException());

        Assert.Equal(StartupCompactionDecision.NotSQLite, decision);
        database.VerifyNoOtherCalls();
    }

    #endregion

    #region Decision

    [Theory]
    // Only after an upgrade that ran schema steps.
    [InlineData(false, 2 * GB, GB, 100 * GB, nameof(StartupCompactionDecision.NoUpgrade))]
    [InlineData(true, 2 * GB, GB, 100 * GB, nameof(StartupCompactionDecision.Compact))]
    // Only when the free pages hold more than a tenth of the database or more than 100 MB.
    [InlineData(true, 1000 * MB, 100 * MB, 100 * GB, nameof(StartupCompactionDecision.TooLittleToReclaim))]
    [InlineData(true, 500 * MB, 10 * MB, 100 * GB, nameof(StartupCompactionDecision.TooLittleToReclaim))]
    [InlineData(true, 0, 0, 100 * GB, nameof(StartupCompactionDecision.TooLittleToReclaim))]
    [InlineData(true, 1000 * MB, 100 * MB + 1, 100 * GB, nameof(StartupCompactionDecision.Compact))]
    [InlineData(true, 10 * GB, 100 * MB + 1, 100 * GB, nameof(StartupCompactionDecision.Compact))]
    [InlineData(true, 10 * GB, 100 * MB, 100 * GB, nameof(StartupCompactionDecision.TooLittleToReclaim))]
    // Only when the drive has twice the database's size free, which is checked last.
    [InlineData(true, 2 * GB, GB, 4 * GB - 1, nameof(StartupCompactionDecision.NotEnoughDiskSpace))]
    [InlineData(true, 2 * GB, GB, 4 * GB, nameof(StartupCompactionDecision.Compact))]
    [InlineData(true, 2 * GB, GB, null, nameof(StartupCompactionDecision.DiskSpaceUnknown))]
    [InlineData(true, 2 * GB, MB, 0L, nameof(StartupCompactionDecision.TooLittleToReclaim))]
    public void TheDecisionFollowsTheUpgradeTheFreePagesAndTheDisk(bool ranSchemaSteps, long databaseSize, long reclaimable, long? availableDiskSpace, string expected)
        => Assert.Equal(Enum.Parse<StartupCompactionDecision>(expected), StartupCompaction.Decide(ranSchemaSteps, databaseSize, reclaimable, availableDiskSpace));

    #endregion

    #region Disk Space

    [Fact]
    public void AnEmptyPathHasNoFreeSpaceToRead()
        => Assert.Null(StartupCompaction.GetAvailableDiskSpace(string.Empty));

    #endregion
}
