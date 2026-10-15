using System.Collections.Generic;
using Shoko.TestData.Schema;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// The type mapping <see cref="SchemaTypeParityTests"/> compares through.
/// </summary>
/// <remarks>
/// The reduction to a family decides what counts as a divergence: fold two together and a real
/// difference stops being reported. Needs no dumps, so this runs on every pull request.
/// </remarks>
public class SchemaTypeMappingTests
{
    #region Families

    [Theory]
    // The same intent, spelled by SQLite, MySQL and SQL Server in turn.
    [InlineData("integer", "int", "int")]
    [InlineData("text", "varchar", "nvarchar")]
    [InlineData("text", "longtext", "nvarchar")]
    [InlineData("datetime", "datetime", "datetime2")]
    [InlineData("date", "date", "date")]
    [InlineData("real", "decimal", "decimal")]
    [InlineData("blob", "blob", "varbinary")]
    [InlineData("uniqueidentifier", "char", "uniqueidentifier")]
    public void TheSameIntentReducesToTheSameFamily(string sqlite, string mySql, string sqlServer)
    {
        var family = SchemaSnapshot.FamilyOf(sqlite);

        Assert.Equal(family, SchemaSnapshot.FamilyOf(mySql));
        Assert.Equal(family, SchemaSnapshot.FamilyOf(sqlServer));
    }

    [Theory]
    // Types that must stay apart, or a column silently changing between them goes unreported.
    [InlineData("int", "varchar")]
    [InlineData("int", "datetime")]
    [InlineData("int", "bigint")]
    [InlineData("date", "datetime")]
    [InlineData("decimal", "int")]
    [InlineData("varbinary", "nvarchar")]
    public void DifferentIntentsReduceToDifferentFamilies(string one, string other)
        => Assert.NotEqual(SchemaSnapshot.FamilyOf(one), SchemaSnapshot.FamilyOf(other));

    [Fact]
    public void TheDialectSpellingIsIgnored()
    {
        // Casing and padding come straight from the catalog.
        Assert.Equal(SchemaSnapshot.FamilyOf("int"), SchemaSnapshot.FamilyOf("  INT "));
        Assert.Equal(SchemaSnapshot.FamilyOf("nvarchar"), SchemaSnapshot.FamilyOf("NVarChar"));
    }

    [Fact]
    public void AnUnrecognisedTypeKeepsItsOwnName()
    {
        // Folding it into an existing family would make it compare equal to an unrelated type.
        Assert.Equal("hyperloop", SchemaSnapshot.FamilyOf("HyperLoop"));
        Assert.NotEqual(SchemaSnapshot.FamilyOf("int"), SchemaSnapshot.FamilyOf("hyperloop"));
    }

    #endregion

    #region What SQLite is excused

    [Theory]
    [InlineData("integer", "bigint", "bigint", true)] // its INTEGER already holds 64 bits; there is no BIGINT to require
    [InlineData("integer", "integer", "bigint", false)] // both of these have a BIGINT and can say so
    [InlineData("text", "integer", "integer", false)]
    [InlineData("datetime", "text", "text", false)]
    [InlineData("", "text", "text", false)]
    // Absence is the column comparison's business; the backends that do have the column must still agree.
    [InlineData("integer", null, null, true)]
    [InlineData(null, "integer", "text", false)]
    public void TheBackendsAgreeOnlyWhereSqliteHadNoTypeToDeclare(string? sqlite, string? mySql, string? sqlServer, bool expected)
        => Assert.Equal(expected, Observed(sqlite, mySql, sqlServer));

    private static bool Observed(string? sqlite, string? mySql, string? sqlServer)
    {
        var observed = new Dictionary<string, string>();
        if (sqlite is not null) observed["SQLite"] = SchemaSnapshot.FamilyOf(sqlite);
        if (mySql is not null) observed["MySQL"] = SchemaSnapshot.FamilyOf(mySql);
        if (sqlServer is not null) observed["SQLServer"] = SchemaSnapshot.FamilyOf(sqlServer);

        return SchemaSnapshot.FamiliesAgree(observed);
    }

    #endregion
}
