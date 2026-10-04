using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Shoko.Server.Databases;

/// <summary>
///   Rebuilds the SQLite file once an upgrade's schema steps and data fixes
///   are done, when the space they freed is worth giving back. A normal start,
///   and the other backends, are never compacted.
/// </summary>
internal static class StartupCompaction
{
    #region Thresholds

    /// <summary>
    ///   The free pages must hold more than this share of the database, or
    ///   more than <see cref="MinimumReclaimableBytes"/>, to compact it.
    /// </summary>
    internal const int MinimumReclaimablePercent = 10;

    /// <summary>
    ///   The free pages must hold more than this many bytes, or more than
    ///   <see cref="MinimumReclaimablePercent"/> of the database, to compact it.
    /// </summary>
    internal const long MinimumReclaimableBytes = 100L * 1024 * 1024;

    /// <summary>
    ///   How many times the database's size its drive must have free: the
    ///   rebuilt copy, then the write-ahead log it goes through.
    /// </summary>
    internal const int RequiredFreeSpaceFactor = 2;

    #endregion

    #region Decision

    /// <summary>
    ///   Decides whether the start-up compacts the database.
    /// </summary>
    /// <param name="ranSchemaSteps">Whether this start ran schema steps, the same condition that backs the database up.</param>
    /// <param name="databaseSize">The database's size in bytes: its page count times its page size.</param>
    /// <param name="reclaimableBytes">The bytes its free pages hold: the free page count times the page size.</param>
    /// <param name="availableDiskSpace">The free bytes on the database's drive, or <c>null</c> when they could not be read.</param>
    /// <returns>What the start-up does with the database.</returns>
    internal static StartupCompactionDecision Decide(bool ranSchemaSteps, long databaseSize, long reclaimableBytes, long? availableDiskSpace)
    {
        if (!ranSchemaSteps)
            return StartupCompactionDecision.NoUpgrade;

        if (reclaimableBytes * 100 <= databaseSize * MinimumReclaimablePercent && reclaimableBytes <= MinimumReclaimableBytes)
            return StartupCompactionDecision.TooLittleToReclaim;

        if (availableDiskSpace is not { } available)
            return StartupCompactionDecision.DiskSpaceUnknown;

        if (available < databaseSize * RequiredFreeSpaceFactor)
            return StartupCompactionDecision.NotEnoughDiskSpace;

        return StartupCompactionDecision.Compact;
    }

    #endregion

    #region Run

    /// <summary>
    ///   Compacts an SQLite database after an upgrade when <see cref="Decide"/>
    ///   says so. It never throws: a failure is logged and leaves the file as
    ///   it was.
    /// </summary>
    /// <param name="database">The core's database.</param>
    /// <param name="ranSchemaSteps">Whether this start ran schema steps; nothing is read or written when it did not.</param>
    /// <param name="logger">Told the sizes, the duration, and why a compaction was skipped or failed.</param>
    /// <param name="reportProgress">Given the start-up message while the file is rebuilt.</param>
    /// <returns>What was done with the database.</returns>
    internal static StartupCompactionDecision Run(IDatabase database, bool ranSchemaSteps, ILogger logger, Action<string> reportProgress)
    {
        if (!ranSchemaSteps)
            return StartupCompactionDecision.NoUpgrade;

        if (database is not SQLite sqlite)
            return StartupCompactionDecision.NotSQLite;

        try
        {
            var before = sqlite.MeasureSpace();
            var available = GetAvailableDiskSpace(before.FilePath);
            var decision = Decide(ranSchemaSteps, before.DatabaseSize, before.ReclaimableBytes, available);
            switch (decision)
            {
                case StartupCompactionDecision.TooLittleToReclaim:
                    logger.LogInformation(
                        "Not compacting the database after the upgrade: its free pages hold {Reclaimable:N1} MB of {Size:N1} MB",
                        ToMegabytes(before.ReclaimableBytes), ToMegabytes(before.DatabaseSize)
                    );
                    return decision;

                case StartupCompactionDecision.DiskSpaceUnknown:
                    logger.LogInformation(
                        "Not compacting the database after the upgrade: the free space on the drive of {Path} could not be read",
                        before.FilePath
                    );
                    return decision;

                case StartupCompactionDecision.NotEnoughDiskSpace:
                    logger.LogInformation(
                        "Not compacting the database after the upgrade: its drive has {Available:N1} MB free and it needs {Required:N1} MB; " +
                        "run the Vacuum Database action once there is room to give back {Reclaimable:N1} MB",
                        ToMegabytes(available!.Value), ToMegabytes(before.DatabaseSize * RequiredFreeSpaceFactor), ToMegabytes(before.ReclaimableBytes)
                    );
                    return decision;
            }

            reportProgress("Compacting database...");
            logger.LogInformation(
                "Compacting the database after the upgrade: {Size:N1} MB on disk, {Reclaimable:N1} MB of it in free pages",
                ToMegabytes(before.SizeOnDisk), ToMegabytes(before.ReclaimableBytes)
            );

            var stopwatch = Stopwatch.StartNew();
            sqlite.Vacuum();
            stopwatch.Stop();

            var after = sqlite.MeasureSpace();
            logger.LogInformation(
                "Compacted the database from {Before:N1} MB to {After:N1} MB on disk in {Duration}",
                ToMegabytes(before.SizeOnDisk), ToMegabytes(after.SizeOnDisk), stopwatch.Elapsed
            );
            return decision;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to compact the database after the upgrade; it was left as it was");
            return StartupCompactionDecision.Failed;
        }
    }

    /// <summary>
    ///   Reads the free space on the drive holding a file.
    /// </summary>
    /// <param name="filePath">The file's path.</param>
    /// <returns>The free bytes, or <c>null</c> when they could not be read.</returns>
    internal static long? GetAvailableDiskSpace(string filePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
            return string.IsNullOrEmpty(directory) ? null : new DriveInfo(directory).AvailableFreeSpace;
        }
        catch
        {
            return null;
        }
    }

    private static double ToMegabytes(long bytes)
        => bytes / 1024d / 1024d;

    #endregion
}

/// <summary>
///   What the start-up did with the database after the upgrade.
/// </summary>
internal enum StartupCompactionDecision
{
    /// <summary>
    ///   No schema step ran, so the database was left alone.
    /// </summary>
    NoUpgrade,

    /// <summary>
    ///   The backend is not SQLite, which is the only one compacted on start.
    /// </summary>
    NotSQLite,

    /// <summary>
    ///   The free pages held too little to be worth rebuilding the file.
    /// </summary>
    TooLittleToReclaim,

    /// <summary>
    ///   The free space on the database's drive could not be read.
    /// </summary>
    DiskSpaceUnknown,

    /// <summary>
    ///   The database's drive had less free space than twice its size.
    /// </summary>
    NotEnoughDiskSpace,

    /// <summary>
    ///   The file was rebuilt.
    /// </summary>
    Compact,

    /// <summary>
    ///   Measuring or rebuilding the file failed, which left it as it was.
    /// </summary>
    Failed,
}

/// <summary>
///   The sizes of an SQLite database.
/// </summary>
/// <param name="FilePath">The path of the database's file.</param>
/// <param name="DatabaseSize">The page count times the page size, in bytes.</param>
/// <param name="ReclaimableBytes">The free page count times the page size.</param>
/// <param name="SizeOnDisk">The file and its write-ahead log together, in bytes.</param>
internal sealed record SQLiteSpace(string FilePath, long DatabaseSize, long ReclaimableBytes, long SizeOnDisk);
