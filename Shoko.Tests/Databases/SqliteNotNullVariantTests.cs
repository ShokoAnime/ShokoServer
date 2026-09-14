using System;
using Shoko.Server.Databases;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// <see cref="SQLite.NotNullVariantOf(string, string)"/>, which rewrites a table's own
/// <c>CREATE TABLE</c> to tighten one column.
/// </summary>
/// <remarks>
/// SQLite cannot alter a column in place, so the migration rebuilds the table around a patched
/// <c>CREATE TABLE</c>. Getting the patch wrong loses a column, a default or the primary key, and the
/// shapes it must cope with differ by upgrade path — see
/// <see cref="SQLite.NotNullVariantOf(string, string)"/>.
/// </remarks>
public class SqliteNotNullVariantTests
{
    // As a database migrating in one pass still has it: a PostDatabaseFix drops these later.
    private const string VideoLocalBeforeTheHashColumnsAreDropped = """
        CREATE TABLE VideoLocal (
            VideoLocalID INTEGER PRIMARY KEY AUTOINCREMENT,
            Hash TEXT NOT NULL,
            CRC32 TEXT NULL, MD5 TEXT NULL,
            SHA1 TEXT NULL,
            FileSize INTEGER NOT NULL,
            DateTimeUpdated DATETIME NOT NULL,
            FileName TEXT NOT NULL DEFAULT '',
            DateTimeCreated DATETIME NULL,
            MediaBlob BLOB NULL
        )
        """;

    // As a database that migrated earlier already has it.
    private const string VideoLocalAfterTheHashColumnsAreDropped = """
        CREATE TABLE VideoLocal (
            VideoLocalID INTEGER PRIMARY KEY AUTOINCREMENT,
            Hash TEXT NOT NULL,
            FileSize INTEGER NOT NULL,
            DateTimeUpdated DATETIME NOT NULL,
            FileName TEXT NOT NULL DEFAULT '',
            DateTimeCreated DATETIME NULL,
            MediaBlob BLOB NULL
        )
        """;

    private const string AniDBAnimeTitle =
        "CREATE TABLE AniDB_Anime_Title ( AniDB_Anime_TitleID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeID INTEGER NOT NULL, TitleType TEXT NOT NULL, Language TEXT NOT NULL, Title TEXT NULL )";

    #region The two columns the migration tightens

    [Theory]
    [InlineData(VideoLocalBeforeTheHashColumnsAreDropped)]
    [InlineData(VideoLocalAfterTheHashColumnsAreDropped)]
    public void TheColumnIsTightenedWhicheverShapeTheTableIsIn(string createCommand)
    {
        var patched = SQLite.NotNullVariantOf(createCommand, "DateTimeCreated");

        Assert.Contains("DateTimeCreated DATETIME NOT NULL", patched);
        Assert.DoesNotContain("DateTimeCreated DATETIME NULL", patched);
    }

    [Fact]
    public void TheTitleColumnIsTightened()
    {
        var patched = SQLite.NotNullVariantOf(AniDBAnimeTitle, "Title");

        Assert.Contains("Title TEXT NOT NULL )", patched);
    }

    #endregion

    #region Everything else is left alone

    [Theory]
    [InlineData(VideoLocalBeforeTheHashColumnsAreDropped, "DateTimeCreated")]
    [InlineData(VideoLocalAfterTheHashColumnsAreDropped, "DateTimeCreated")]
    [InlineData(AniDBAnimeTitle, "Title")]
    public void EveryOtherColumnSurvivesUnchanged(string createCommand, string columnName)
    {
        var before = Columns(createCommand);
        var after = Columns(SQLite.NotNullVariantOf(createCommand, columnName));

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
    public void ThePrimaryKeyAndDefaultsAreKept()
    {
        var patched = SQLite.NotNullVariantOf(VideoLocalAfterTheHashColumnsAreDropped, "DateTimeCreated");

        Assert.Contains("VideoLocalID INTEGER PRIMARY KEY AUTOINCREMENT", patched);
        Assert.Contains("FileName TEXT NOT NULL DEFAULT ''", patched);
    }

    [Fact]
    public void AColumnWhoseNameContainsAnotherIsNotConfusedForIt()
    {
        var patched = SQLite.NotNullVariantOf(
            "CREATE TABLE T ( DateTimeCreatedRaw TEXT NULL, DateTimeCreated DATETIME NULL, XDateTimeCreated TEXT NULL )",
            "DateTimeCreated");

        Assert.Contains("DateTimeCreatedRaw TEXT NULL", patched);
        Assert.Contains("XDateTimeCreated TEXT NULL", patched);
        Assert.Contains("DateTimeCreated DATETIME NOT NULL", patched);
    }

    #endregion

    #region Shapes it still has to handle

    [Fact]
    public void AColumnStatingNeitherNullNorNotNullIsStillTightened()
        // SQLite treats an unstated column as nullable.
        => Assert.Contains("LastAVDumped DATETIME NOT NULL",
            SQLite.NotNullVariantOf("CREATE TABLE T ( Hash TEXT NOT NULL, LastAVDumped DATETIME )", "LastAVDumped"));

    [Fact]
    public void ASizedTypeKeepsItsSize()
        // The size contains a comma, which is also what separates columns.
        => Assert.Contains("Rating decimal(6,2) NOT NULL",
            SQLite.NotNullVariantOf("CREATE TABLE T ( Rating decimal(6,2) NULL, Votes INTEGER NOT NULL )", "Rating"));

    [Fact]
    public void AColumnThatIsAlreadyNotNullIsLeftAsItIs()
    {
        const string createCommand = "CREATE TABLE T ( Hash TEXT NOT NULL, Votes INTEGER NOT NULL )";

        Assert.Equal(createCommand, SQLite.NotNullVariantOf(createCommand, "Hash"));
    }

    [Fact]
    public void AColumnThatIsNotThereIsAnError()
        // Returning it unchanged would rebuild the table untightened and report success.
        => Assert.Throws<InvalidOperationException>(
            () => SQLite.NotNullVariantOf("CREATE TABLE T ( Hash TEXT NOT NULL )", "Nonexistent"));

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
