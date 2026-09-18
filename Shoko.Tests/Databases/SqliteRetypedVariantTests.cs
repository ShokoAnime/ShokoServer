using System;
using Shoko.Server.Databases;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// <see cref="SQLite.RetypedVariantOf(string, string, string)"/>, which rewrites a table's own
/// <c>CREATE TABLE</c> to give one column a different type.
/// </summary>
/// <remarks>
/// SQLite cannot retype a column in place, so the migration rebuilds the table around a patched
/// <c>CREATE TABLE</c>. Only the type may be replaced: the constraints that follow it carry the
/// nullability, the default and the primary key, and dropping one of those loses data or rejects
/// rows the table used to accept.
/// </remarks>
public class SqliteRetypedVariantTests
{
    private const string AniDBAnime =
        "CREATE TABLE AniDB_Anime ( AniDB_AnimeID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID INTEGER NOT NULL, AirDate DATETIME NULL, EndDate DATETIME NULL, MainTitle TEXT NOT NULL )";

    // As an ALTER TABLE that named no type leaves it.
    private const string AnimeSeriesUser =
        "CREATE TABLE AnimeSeries_User ( AnimeSeries_UserID INTEGER PRIMARY KEY AUTOINCREMENT, JMMUserID INTEGER NOT NULL, WatchedDate DATETIME, UserTags NOT NULL DEFAULT '' )";

    private const string TmdbEpisode =
        "CREATE TABLE TMDB_Episode ( TMDB_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, EpisodeNumber INTEGER NOT NULL, Runtime TEXT NULL, UserRating REAL NOT NULL )";

    // As a tool that rebuilt the table outside Shoko, such as DB Browser for SQLite, leaves it.
    private const string QuotedVideoLocal = """
        CREATE TABLE "VideoLocal" (
            "VideoLocalID"    INTEGER,
            "Hash"    text NOT NULL,
            "FileSize"    int NOT NULL,
            "DateTimeUpdated"    datetime NOT NULL,
            "DateTimeCreated"    datetime,
            PRIMARY KEY("VideoLocalID" AUTOINCREMENT)
        )
        """;

    #region The columns the migration retypes

    [Fact]
    public void ADeclaredTypeIsReplaced()
        => Assert.Contains("AirDate varchar(10) NULL", SQLite.RetypedVariantOf(AniDBAnime, "AirDate", "varchar(10)"));

    [Fact]
    public void AColumnWithNoTypeAtAllIsGivenOne()
    {
        var patched = SQLite.RetypedVariantOf(AnimeSeriesUser, "UserTags", "TEXT");

        Assert.Contains("UserTags TEXT NOT NULL DEFAULT ''", patched);
    }

    [Fact]
    public void ATextColumnBecomesAnIntegerOne()
        => Assert.Contains("Runtime INTEGER NULL", SQLite.RetypedVariantOf(TmdbEpisode, "Runtime", "INTEGER"));

    [Fact]
    public void AQuotedIdentifierIsRetyped()
        => Assert.Contains("\"DateTimeCreated\" DATETIME,", SQLite.RetypedVariantOf(QuotedVideoLocal, "DateTimeCreated", "DATETIME"));

    [Fact]
    public void RetypingTwiceRetypesBothColumns()
    {
        var patched = SQLite.RetypedVariantOf(SQLite.RetypedVariantOf(AniDBAnime, "AirDate", "varchar(10)"), "EndDate", "varchar(10)");

        Assert.Contains("AirDate varchar(10) NULL", patched);
        Assert.Contains("EndDate varchar(10) NULL", patched);
    }

    #endregion

    #region Everything else is left alone

    [Theory]
    [InlineData(AniDBAnime, "AirDate", "varchar(10)")]
    [InlineData(AnimeSeriesUser, "UserTags", "TEXT")]
    [InlineData(TmdbEpisode, "Runtime", "INTEGER")]
    public void EveryOtherColumnSurvivesUnchanged(string createCommand, string columnName, string type)
    {
        var before = Columns(createCommand);
        var after = Columns(SQLite.RetypedVariantOf(createCommand, columnName, type));

        // The rebuild copies column by column, so a lost column loses its data with it.
        Assert.Equal(before.Length, after.Length);
        for (var i = 0; i < before.Length; i++)
        {
            if (before[i].StartsWith(columnName + " ", StringComparison.Ordinal))
                continue;

            Assert.Equal(before[i], after[i]);
        }
    }

    [Fact]
    public void ThePrimaryKeyAndConstraintsAreKept()
    {
        var patched = SQLite.RetypedVariantOf(AnimeSeriesUser, "UserTags", "TEXT");

        Assert.Contains("AnimeSeries_UserID INTEGER PRIMARY KEY AUTOINCREMENT", patched);
        Assert.Contains("NOT NULL DEFAULT ''", patched);
    }

    [Fact]
    public void AColumnWhoseNameContainsAnotherIsNotConfusedForIt()
    {
        var patched = SQLite.RetypedVariantOf(
            "CREATE TABLE T ( AirDateRaw TEXT NULL, AirDate DATETIME NULL, LatestEpisodeAirDate DATETIME NULL )",
            "AirDate",
            "varchar(10)");

        Assert.Contains("AirDateRaw TEXT NULL", patched);
        Assert.Contains("LatestEpisodeAirDate DATETIME NULL", patched);
        Assert.Contains("AirDate varchar(10) NULL", patched);
    }

    #endregion

    #region Shapes it still has to handle

    [Fact]
    public void ASizedTypeIsReplacedWhole()
        // The size contains a comma, which is also what separates columns.
        => Assert.Contains("Rating INTEGER NOT NULL",
            SQLite.RetypedVariantOf("CREATE TABLE T ( Rating decimal(6,2) NOT NULL, Votes INTEGER NOT NULL )", "Rating", "INTEGER"));

    [Fact]
    public void AMultiWordTypeIsReplacedWhole()
        => Assert.Contains("FileSize INTEGER NOT NULL",
            SQLite.RetypedVariantOf("CREATE TABLE T ( FileSize UNSIGNED BIG INT NOT NULL )", "FileSize", "INTEGER"));

    [Fact]
    public void AColumnStatingNoNullabilityKeepsStatingNone()
        // SQLite treats an unstated column as nullable, and saying NULL here would be a change.
        => Assert.Contains("LastAVDumped varchar(10) )",
            SQLite.RetypedVariantOf("CREATE TABLE T ( Hash TEXT NOT NULL, LastAVDumped DATETIME )", "LastAVDumped", "varchar(10)"));

    [Fact]
    public void AColumnAlreadyOfThatTypeIsLeftAsItIs()
    {
        const string createCommand = "CREATE TABLE T ( Hash TEXT NOT NULL, Votes INTEGER NOT NULL )";

        Assert.Equal(createCommand, SQLite.RetypedVariantOf(createCommand, "Hash", "TEXT"));
    }

    [Fact]
    public void AColumnThatIsNotThereIsAnError()
        // Returning it unchanged would rebuild the table unretyped and report success.
        => Assert.Throws<InvalidOperationException>(
            () => SQLite.RetypedVariantOf("CREATE TABLE T ( Hash TEXT NOT NULL )", "Nonexistent", "INTEGER"));

    #endregion

    private static string[] Columns(string createCommand)
    {
        var body = createCommand[(createCommand.IndexOf('(') + 1)..createCommand.LastIndexOf(')')];
        var columns = new System.Collections.Generic.List<string>();
        var depth = 0;
        var current = new System.Text.StringBuilder();
        foreach (var character in body)
        {
            if (character is '(') depth++;
            if (character is ')') depth--;
            if (character is ',' && depth is 0)
            {
                columns.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        columns.Add(current.ToString().Trim());

        return [.. columns];
    }
}
