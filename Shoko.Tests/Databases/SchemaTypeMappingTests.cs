using System.Collections.Generic;
using Shoko.TestData.Schema;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// The type mapping <see cref="SchemaTypeParityTests"/> compares through.
/// </summary>
/// <remarks>
/// The three backends spell the same intent differently, so the comparison cannot use the dialect
/// name — it reduces each to a family first. That reduction decides what counts as a divergence, so
/// it is the part most worth getting wrong quietly: fold two families together and a real difference
/// stops being reported, keep two apart and every column of that type is reported forever.
///
/// Needs no schema dumps, so unlike the comparison itself this runs on every pull request.
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
        // Casing and padding come straight from the catalog and vary between backends.
        Assert.Equal(SchemaSnapshot.FamilyOf("int"), SchemaSnapshot.FamilyOf("  INT "));
        Assert.Equal(SchemaSnapshot.FamilyOf("nvarchar"), SchemaSnapshot.FamilyOf("NVarChar"));
    }

    [Fact]
    public void AnUnrecognisedTypeKeepsItsOwnName()
    {
        // Rather than being folded into some existing family, where it would compare equal to a type
        // it has nothing to do with.
        Assert.Equal("hyperloop", SchemaSnapshot.FamilyOf("HyperLoop"));
        Assert.NotEqual(SchemaSnapshot.FamilyOf("int"), SchemaSnapshot.FamilyOf("hyperloop"));
    }

    #endregion

    #region What SQLite is excused

    [Fact]
    public void SqliteMayDeclareIntegerWhereTheOthersDeclareBigint()
        // SQLite has no BIGINT — its INTEGER already holds 64 bits — so requiring one would be
        // requiring a type that does not exist.
        => Assert.True(Observed(sqlite: "integer", mySql: "bigint", sqlServer: "bigint"));

    [Fact]
    public void TheOtherBackendsMayNotDisagreeWithEachOther()
        // The leniency is SQLite's alone: both of these have a BIGINT and can say so.
        => Assert.False(Observed(sqlite: "integer", mySql: "integer", sqlServer: "bigint"));

    [Theory]
    [InlineData("text", "integer", "integer")]
    [InlineData("datetime", "text", "text")]
    [InlineData("", "text", "text")]
    public void SqliteIsNotExcusedATypeItCouldHaveDeclared(string sqlite, string mySql, string sqlServer)
        // Nothing stops SQLite declaring INTEGER, TEXT or DATETIME, so differing there is a real
        // divergence and not an absence in its type system.
        => Assert.False(Observed(sqlite, mySql, sqlServer));

    [Fact]
    public void AColumnMissingFromABackendDoesNotCountAsAgreement()
    {
        // Absence is reported by the column comparison; this one only judges the backends that have
        // the column, and two of them still have to agree.
        Assert.True(Observed(sqlite: "integer", mySql: null, sqlServer: null));
        Assert.False(Observed(sqlite: null, mySql: "integer", sqlServer: "text"));
    }

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
