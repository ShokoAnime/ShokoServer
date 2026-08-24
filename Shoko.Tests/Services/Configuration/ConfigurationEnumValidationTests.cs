using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Validation;
using Shoko.Server.Services.Configuration;
using Xunit;

using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Shoko.Tests.Services.Configuration;

/// <summary>
/// Unit tests covering how <see cref="JsonSchemaValidatorBase"/> matches string enum members, and the assumption it
/// leans on: that the JSON deserializers used for configurations match those members case-insensitively.
/// </summary>
public class ConfigurationEnumValidationTests
{
    private const string PropertyName = "DatabaseType";

    private static readonly string[] _enumeration = ["SQLite", "SQLServer", "MSSQL", "MySQL", "MariaDB"];

    private static JsonSchema CreateSchema()
    {
        var property = new JsonSchemaProperty { Type = JsonObjectType.String };
        foreach (var value in _enumeration)
            property.Enumeration.Add(value);

        var schema = new JsonSchema { Type = JsonObjectType.Object };
        schema.Properties.Add(PropertyName, property);
        return schema;
    }

    private static string CreateJson(string value)
        => $$"""{"{{PropertyName}}": "{{value}}"}""";

    [Theory]
    [InlineData("SQLite")]
    [InlineData("SQLServer")]
    [InlineData("MySQL")]
    [InlineData("MySql")]
    [InlineData("mysql")]
    [InlineData("MYSQL")]
    [InlineData("mariadb")]
    [InlineData("mssql")]
    public void ValidateEnum_AcceptsAnyCasing(string value)
    {
        var (_, errors) = new JsonSchemaValidatorBase().Validate(CreateJson(value), CreateSchema());

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("My SQL")]
    [InlineData("")]
    public void ValidateEnum_RejectsValuesOutsideTheEnumeration(string value)
    {
        var (_, errors) = new JsonSchemaValidatorBase().Validate(CreateJson(value), CreateSchema());

        var error = Assert.Single(errors);
        Assert.Equal(ValidationErrorKind.NotInEnumeration, error.Kind);
    }

    [Fact]
    public void ValidateEnum_DoesNotNormalizeByDefault()
    {
        var (token, errors) = new JsonSchemaValidatorBase().Validate(CreateJson("mysql"), CreateSchema());

        Assert.Empty(errors);
        Assert.Equal("mysql", token[PropertyName]!.ToString());
    }

    [Theory]
    [InlineData("SQLite")]
    [InlineData("MySQL")]
    [InlineData("MSSQL")]
    public void ValidateEnum_SkipsNormalizationForExactMatches(string value)
    {
        var validator = new NormalizingValidator();

        var (token, errors) = validator.Validate(CreateJson(value), CreateSchema());

        Assert.Empty(errors);
        Assert.Empty(validator.Normalized);
        Assert.Equal(value, token[PropertyName]!.ToString());
    }

    [Theory]
    [InlineData("mysql", "MySQL")]
    [InlineData("MySql", "MySQL")]
    [InlineData("MYSQL", "MySQL")]
    [InlineData("mariadb", "MariaDB")]
    [InlineData("mssql", "MSSQL")]
    [InlineData("sqlite", "SQLite")]
    public void ValidateEnum_NormalizesCasingToTheDeclaredMember(string value, string expected)
    {
        var validator = new NormalizingValidator();

        var (token, errors) = validator.Validate(CreateJson(value), CreateSchema());

        Assert.Empty(errors);
        Assert.Equal((value, expected), Assert.Single(validator.Normalized));
        Assert.Equal(expected, token[PropertyName]!.ToString());
    }

    private sealed class NormalizingValidator : JsonSchemaValidatorBase
    {
        public List<(string Value, string EnumerationValue)> Normalized { get; } = [];

        protected override void NormalizeEnum(JToken token, string enumerationValue, JsonSchema schema, string? propertyName, string propertyPath)
        {
            Normalized.Add((token.ToString(), enumerationValue));
            token.Replace(new JValue(enumerationValue));
        }
    }

    #region Deserializer Assumptions

    public enum TestDatabaseType
    {
        SQLite = 0,
        SQLServer = 1,
        MSSQL = SQLServer,
        MySQL = 2,
        MariaDB = MySQL,
    }

    public class TestConfiguration
    {
        public TestDatabaseType DatabaseType { get; set; }
    }

    private static readonly JsonSerializerSettings _newtonsoftSettings = new() { Converters = [new StringEnumConverter()] };

    private static readonly JsonSerializerOptions _systemTextJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Theory]
    [InlineData("MySQL", TestDatabaseType.MySQL)]
    [InlineData("MySql", TestDatabaseType.MySQL)]
    [InlineData("mysql", TestDatabaseType.MySQL)]
    [InlineData("MYSQL", TestDatabaseType.MySQL)]
    [InlineData("mariadb", TestDatabaseType.MariaDB)]
    [InlineData("mssql", TestDatabaseType.MSSQL)]
    [InlineData("sqlite", TestDatabaseType.SQLite)]
    public void Deserializers_MatchStringEnumMembersCaseInsensitively(string value, TestDatabaseType expected)
    {
        var json = CreateJson(value);

        Assert.Equal(expected, JsonConvert.DeserializeObject<TestConfiguration>(json, _newtonsoftSettings)!.DatabaseType);
        Assert.Equal(expected, JsonSerializer.Deserialize<TestConfiguration>(json, _systemTextJsonOptions)!.DatabaseType);
    }

    [Fact]
    public void Deserializers_MatchPropertyNamesCaseInsensitively()
    {
        const string json = """{"databasetype": "MySQL"}""";

        Assert.Equal(TestDatabaseType.MySQL, JsonConvert.DeserializeObject<TestConfiguration>(json, _newtonsoftSettings)!.DatabaseType);
        Assert.Equal(TestDatabaseType.MySQL, JsonSerializer.Deserialize<TestConfiguration>(json, _systemTextJsonOptions)!.DatabaseType);
    }

    #endregion
}
