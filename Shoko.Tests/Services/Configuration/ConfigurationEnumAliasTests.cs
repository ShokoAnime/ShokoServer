using System.Collections.Generic;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services.Configuration;

/// <summary>
/// Unit tests for <see cref="ShokoJsonSchemaGenerator.TryResolveEnumAlias"/>, which resolves the alias spellings the
/// generator records for an enum member back to the member itself.
/// </summary>
public class ConfigurationEnumAliasTests
{
    private static List<Dictionary<string, string>> CreateDefinitions() =>
    [
        new() { { "value", "SQLite" }, { "aliasValues", string.Empty } },
        new() { { "value", "SQLServer" }, { "aliasValues", "MSSQL, MsSqlServer" } },
        new() { { "value", "MySQL" }, { "aliasValues", "MariaDB" } },
    ];

    [Theory]
    [InlineData("MSSQL", "SQLServer")]
    [InlineData("mssql", "SQLServer")]
    [InlineData("MsSqlServer", "SQLServer")]
    [InlineData("mssqlserver", "SQLServer")]
    [InlineData("MariaDB", "MySQL")]
    [InlineData("mariadb", "MySQL")]
    public void TryResolveEnumAlias_ResolvesAliasesRegardlessOfCasing(string value, string expected)
    {
        Assert.True(ShokoJsonSchemaGenerator.TryResolveEnumAlias(CreateDefinitions(), value, out var enumValue));
        Assert.Equal(expected, enumValue);
    }

    [Theory]
    [InlineData("SQLite")]
    [InlineData("MySQL")]
    [InlineData("bogus")]
    [InlineData("")]
    public void TryResolveEnumAlias_LeavesNonAliasesAlone(string value)
    {
        Assert.False(ShokoJsonSchemaGenerator.TryResolveEnumAlias(CreateDefinitions(), value, out var enumValue));
        Assert.Null(enumValue);
    }
}
