using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shoko.Server.Databases;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// Checks the numbering of each backend's schema steps, and that the core's old
/// AniList tables are neither created nor left behind.
/// </summary>
public class MigrationStepTests
{
    #region Helpers

    /// <summary>
    /// Every table the core kept for AniList before it became a plugin.
    /// </summary>
    private static readonly string[] s_aniListTables =
    [
        "Anilist_Anime",
        "Anilist_Anime_Character",
        "Anilist_Anime_Character_Creator",
        "Anilist_Anime_ExternalLink",
        "Anilist_Anime_Relation",
        "Anilist_Anime_Staff",
        "Anilist_Anime_Studio",
        "Anilist_Anime_Suggestion",
        "Anilist_Anime_Tag",
        "Anilist_Character",
        "Anilist_Creator",
        "Anilist_Episode",
        "Anilist_Studio",
        "Anilist_Tag",
        "CrossRef_AniDB_Anilist_Anime",
        "CrossRef_AniDB_Anilist_Episode",
    ];

    public static TheoryData<string> Backends() => new("SQLite", "MySQL", "SQLServer");

    /// <summary>
    /// The backend's schema steps, in the order they run.
    /// </summary>
    /// <param name="backend">The backend's name, as <see cref="Backends"/> lists it.</param>
    /// <returns>The steps of the backend's <c>_patchCommands</c> list.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The backend is not one of the three.</exception>
    internal static IReadOnlyList<DatabaseCommand> PatchCommands(string backend)
    {
        // MySQL reads the settings singleton while initialising its DDL fields.
        StubSettingsProvider.Install();
        IDatabase database = backend switch
        {
            "SQLite" => new SQLite(null!),
            "MySQL" => new MySQL(null!),
            "SQLServer" => new SQLServer(null!),
            _ => throw new ArgumentOutOfRangeException(nameof(backend)),
        };
        var field = database.GetType().GetField("_patchCommands", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return ((IEnumerable<DatabaseCommand>)field.GetValue(database)!).ToList();
    }

    /// <summary>
    /// The steps of the newest schema version, the one a release has open.
    /// </summary>
    /// <param name="commands">The backend's schema steps.</param>
    /// <returns>The newest version's steps, in the order they run.</returns>
    private static List<DatabaseCommand> NewestVersion(IReadOnlyList<DatabaseCommand> commands)
    {
        var newest = commands.Max(command => command.Version);
        return commands.Where(command => command.Version == newest).ToList();
    }

    #endregion

    #region Numbering

    [Theory]
    [MemberData(nameof(Backends))]
    public void TheNewestVersionCountsUpFromOneWithoutGaps(string backend)
    {
        var revisions = NewestVersion(PatchCommands(backend)).Select(command => command.Revision).ToArray();

        // Development databases are restored from backups rather than patched in place, so the
        // open version is kept contiguous when an unreleased step goes.
        Assert.Equal(Enumerable.Range(1, revisions.Length), revisions);
    }

    #endregion

    #region Old AniList Tables

    [Theory]
    [MemberData(nameof(Backends))]
    public void NoStepCreatesOrReadsAnOldAniListTable(string backend)
    {
        var offending = PatchCommands(backend)
            .Where(command => command.Command is { } sql &&
                sql.Contains("Anilist_", StringComparison.OrdinalIgnoreCase) &&
                !sql.StartsWith("DROP TABLE IF EXISTS", StringComparison.Ordinal))
            .Select(command => $"{command.Version}.{command.Revision}")
            .ToArray();

        Assert.Equal(string.Empty, string.Join(", ", offending));
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public void EveryOldAniListTableIsDropped_OneStepEach(string backend)
    {
        var drops = PatchCommands(backend)
            .Where(command => command.Command?.Contains("Anilist_", StringComparison.OrdinalIgnoreCase) ?? false)
            .Select(command => command.Command!)
            .ToArray();

        Assert.All(drops, sql => Assert.Single(sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
        var dropped = drops
            .Select(sql => sql["DROP TABLE IF EXISTS".Length..].Trim(' ', ';', '`'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(s_aniListTables, dropped);
    }

    #endregion
}
