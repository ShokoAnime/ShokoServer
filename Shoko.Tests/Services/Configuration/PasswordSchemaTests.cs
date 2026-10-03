using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services.Configuration;

/// <summary>
/// Unit tests for how <see cref="ShokoJsonSchemaGenerator"/> marks a secret as a password field: by either of the
/// two attributes the secret masking reads.
/// </summary>
public class PasswordSchemaTests
{
    public class SecretConfiguration : INewtonsoftJsonConfiguration
    {
        [PasswordPropertyText]
        public string? ByPasswordText { get; set; }

        [DataType(DataType.Password)]
        public string? ByDataType { get; set; }

        public string? Plain { get; set; }
    }

    private static string? ElementType(string property)
        => JObject.Parse(
            new ShokoJsonSchemaGenerator(new JsonSerializerSettings { Converters = [new StringEnumConverter()] }, new JsonSerializerOptions())
                .GetSchemaForType(typeof(SecretConfiguration))
                .Schema
                .ToJson()
        )["properties"]![property]!["x-uiDefinition"]?["elementType"]?.Value<string>();

    [Fact]
    public void BothSecretAttributesMakeAPasswordFieldAndNothingElseDoes()
    {
        Assert.Equal("password", ElementType(nameof(SecretConfiguration.ByPasswordText)));
        Assert.Equal("password", ElementType(nameof(SecretConfiguration.ByDataType)));
        Assert.NotEqual("password", ElementType(nameof(SecretConfiguration.Plain)));
    }
}
