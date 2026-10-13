using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Enums;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services.Configuration;

/// <summary>
///   Unit tests for the <c>x-uiDefinition</c> entries
///   <see cref="LegacySchemaUiCompat"/> keeps on a configuration's schema, for
///   the released web UI's dynamic form.
/// </summary>
public class LegacySchemaUiCompatTests
{
    public class LegacyFormConfiguration : IConfiguration
    {
        [Display(Order = 2)]
        [CodeEditor(CodeEditorLanguage.Lua)]
        public string Script { get; set; } = string.Empty;

        [Display(Order = 1)]
        public bool MoveFiles { get; set; }

        [Display(Order = 3)]
        [DataType(DataType.Password)]
        public string Token { get; set; } = string.Empty;

        [Display(Order = 4)]
        public NestedConfiguration Nested { get; set; } = new();
    }

    public class NestedConfiguration
    {
        public int Limit { get; set; }
    }

    private static JObject CreateSchema<TConfig>()
        => JObject.Parse(TestSchemaGenerator.Create().GetSchemaForType(typeof(TConfig)).Schema.ToJson());

    [Fact]
    public void Configuration_KeepsWhatTheOldFormReads()
    {
        var schema = CreateSchema<LegacyFormConfiguration>();
        var root = schema["x-uiDefinition"]!;
        var properties = schema["properties"]!;

        Assert.Equal("field-set", root["sectionType"]!.Value<string>());
        Assert.Equal(["MoveFiles", "Script", "Token", "Nested"], ((JObject)root["structure"]!).Properties().Select(x => x.Name));
        Assert.Equal("auto", properties["MoveFiles"]!["x-uiDefinition"]!["elementType"]!.Value<string>());
        Assert.Equal("code-block", properties["Script"]!["x-uiDefinition"]!["elementType"]!.Value<string>());
        Assert.Equal("Lua", properties["Script"]!["x-uiDefinition"]!["codeLanguage"]!.Value<string>());
        Assert.Equal("password", properties["Token"]!["x-uiDefinition"]!["elementType"]!.Value<string>());
        Assert.Equal("section-container", properties["Nested"]!["x-uiDefinition"]!["elementType"]!.Value<string>());
    }

    [Fact]
    public void ActionParameters_AreLeftWithoutTheLegacyEntries()
    {
        var wrapped = TestSchemaGenerator.Create().GetSchemaForActionParameters(typeof(ParameterisedGlobalAction));

        Assert.Null(JObject.Parse(wrapped.Schema.ToJson())["x-uiDefinition"]);
    }
}
