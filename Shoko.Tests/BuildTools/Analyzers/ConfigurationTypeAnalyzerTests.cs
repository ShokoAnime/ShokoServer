using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Shoko.BuildTools.Analyzers;
using Xunit;

namespace Shoko.Tests.BuildTools.Analyzers;

/// <summary>
/// Tests for <see cref="ConfigurationTypeAnalyzer"/>.
/// </summary>
/// <remarks>
/// The Shoko configuration contract is stubbed into every test compilation instead of referencing
/// <c>Shoko.Abstractions</c>, so the tests stay hermetic and pin the fully qualified names the
/// analyzer matches on.
/// </remarks>
public class ConfigurationTypeAnalyzerTests
{
    private const string Contract = """
        namespace Shoko.Abstractions.Config
        {
            public interface IConfiguration { }
            public interface INewtonsoftJsonConfiguration : IConfiguration { }
        }

        namespace Shoko.Abstractions.Actions
        {
            public interface IExecutableAction { }
        }

        namespace Shoko.Abstractions.Config.Enums
        {
            public enum ConfigurationActionType
            {
                New = 0,
                Validate = 1,
                Save = 2,
                Load = 3,
                LiveEdit = 4,
            }

            public enum ReactiveEventType
            {
                All = 0,
                Edited = 1,
                Focused = 2,
                Unfocused = 3,
            }
        }

        namespace Shoko.Abstractions.Config.Attributes
        {
            using Shoko.Abstractions.Config.Enums;

            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class ConfigurationActionAttribute : System.Attribute
            {
                public ConfigurationActionAttribute(ConfigurationActionType actionType) { ActionType = actionType; }
                public ConfigurationActionType ActionType { get; set; }
                public ReactiveEventType[]? Events { get; set; }
                public string[]? ReactiveMembers { get; set; }
            }
        }

        namespace Shoko.Abstractions.UI.Enums
        {
            public enum UiConditionOperator
            {
                Equals = 0,
                NotEquals = 1,
                IsEmpty = 2,
                IsNotEmpty = 3,
                In = 4,
                NotIn = 5,
                GreaterThan = 6,
                LessThan = 7,
                Contains = 8,
            }

            public enum DisplayListType
            {
                Auto = 0,
                EnumCheckbox = 1,
                ComplexDropdown = 2,
                ComplexTab = 3,
                ComplexInline = 4,
            }
        }

        namespace Shoko.Abstractions.UI.Attributes
        {
            using Shoko.Abstractions.UI.Enums;

            [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
            public class ListAttribute : System.Attribute
            {
                public DisplayListType ListType { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
            public class VisibilityAttribute : System.Attribute
            {
                public string? ToggleWhenMemberIsSet { get; set; }
                public UiConditionOperator ToggleOperator { get; set; }
                public object? ToggleWhenSetTo { get; set; }
                public object?[]? ToggleWhenSetToAny { get; set; }
                public string? DisableWhenMemberIsSet { get; set; }
                public UiConditionOperator DisableOperator { get; set; }
                public object? DisableWhenSetTo { get; set; }
                public object?[]? DisableWhenSetToAny { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class CustomActionAttribute : System.Attribute
            {
                public string? ToggleWhenMemberIsSet { get; set; }
                public UiConditionOperator ToggleOperator { get; set; }
                public object? ToggleWhenSetTo { get; set; }
                public object?[]? ToggleWhenSetToAny { get; set; }
                public string? DisableWhenMemberIsSet { get; set; }
                public UiConditionOperator DisableOperator { get; set; }
                public object? DisableWhenSetTo { get; set; }
                public object?[]? DisableWhenSetToAny { get; set; }
            }
        }
        """;

    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ConfigurationTypeAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestState = { Sources = { Contract, source } },
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    public async Task ListOfList_IsReported()
    {
        await VerifyAsync("""
            using System.Collections.Generic;
            using Shoko.Abstractions.Config;

            public class MyConfig : IConfiguration
            {
                public {|#0:List<List<string>>|} Nested { get; set; } = new();
            }
            """,
            new DiagnosticResult(Diagnostics.NestedCollection)
                .WithLocation(0)
                .WithArguments("Nested", "List<List<string>>", "list"));
    }

    [Fact]
    public async Task DictionaryOfDictionary_IsReported()
    {
        await VerifyAsync("""
            using System.Collections.Generic;
            using Shoko.Abstractions.Config;

            public class MyConfig : IConfiguration
            {
                public {|#0:Dictionary<string, Dictionary<string, int>>|} Nested { get; set; } = new();
            }
            """,
            new DiagnosticResult(Diagnostics.NestedCollection)
                .WithLocation(0)
                .WithArguments("Nested", "Dictionary<string, Dictionary<string, int>>", "dictionary"));
    }

    [Fact]
    public async Task ListOfDictionary_IsReported()
    {
        await VerifyAsync("""
            using System.Collections.Generic;
            using Shoko.Abstractions.Config;

            public class MyConfig : IConfiguration
            {
                public {|#0:List<Dictionary<string, string>>|} Nested { get; set; } = new();
            }
            """,
            new DiagnosticResult(Diagnostics.NestedCollection)
                .WithLocation(0)
                .WithArguments("Nested", "List<Dictionary<string, string>>", "list"));
    }

    [Fact]
    public async Task DictionaryOfCollections_IsNotReported()
    {
        // The two levels get distinct keys ("+Dict" and "+List"), so the
        // generator produces a usable schema. A dictionary of scalar arrays
        // is an ordinary shape and must not be rejected.
        await VerifyAsync("""
            using System.Collections.Generic;
            using Shoko.Abstractions.Config;

            public class MyConfig : IConfiguration
            {
                public Dictionary<string, List<string>> Lists { get; set; } = new();
                public Dictionary<string, string[]> Arrays { get; set; } = new();
            }
            """);
    }

    [Fact]
    public async Task JaggedArray_IsReported()
    {
        await VerifyAsync("""
            using Shoko.Abstractions.Config;

            public class MyConfig : IConfiguration
            {
                public {|#0:string[][]|} Nested { get; set; } = new string[0][];
            }
            """,
            new DiagnosticResult(Diagnostics.NestedCollection)
                .WithLocation(0)
                .WithArguments("Nested", "string[][]", "list"));
    }

    /// <summary>
    /// The case a syntax-only check would miss: the nesting is only visible once the alias is
    /// resolved by the semantic model.
    /// </summary>
    [Fact]
    public async Task AliasedInnerCollection_IsReported()
    {
        await VerifyAsync("""
            using System.Collections.Generic;
            using Shoko.Abstractions.Config;
            using MyAlias = System.Collections.Generic.List<string>;

            public class MyConfig : IConfiguration
            {
                public {|#0:List<MyAlias>|} Nested { get; set; } = new();
            }
            """,
            new DiagnosticResult(Diagnostics.NestedCollection)
                .WithLocation(0)
                .WithArguments("Nested", "List<List<string>>", "list"));
    }

    /// <summary>
    /// The other case a syntax-only check would miss: the nesting only appears once the base
    /// class's type parameter is substituted.
    /// </summary>
    [Fact]
    public async Task GenericBaseSubstitutedToACollection_IsReported()
    {
        await VerifyAsync("""
            using System.Collections.Generic;
            using Shoko.Abstractions.Config;

            public class SectionBase<T>
            {
                public {|#0:List<T>|} Items { get; set; } = new();
            }

            public class MyConfig : SectionBase<List<string>>, IConfiguration
            {
            }
            """,
            new DiagnosticResult(Diagnostics.NestedCollection)
                .WithLocation(0)
                .WithArguments("Items", "List<List<string>>", "list"));
    }

    /// <summary>
    /// The supported way to model two levels: a class in between, which gets its own schema.
    /// </summary>
    [Fact]
    public async Task ListOfClassHoldingAList_IsNotReported()
    {
        await VerifyAsync("""
            using System.Collections.Generic;
            using Shoko.Abstractions.Config;

            public class Section
            {
                public List<string> Values { get; set; } = new();
            }

            public class MyConfig : IConfiguration
            {
                public List<Section> Sections { get; set; } = new();
                public Dictionary<string, Section> Named { get; set; } = new();
                public List<string> Flat { get; set; } = new();
                public string[] Array { get; set; } = new string[0];
                public List<byte[]> Blobs { get; set; } = new();
            }
            """);
    }

    [Fact]
    public async Task NonConfigurationType_IsNotReported()
    {
        await VerifyAsync("""
            using System.Collections.Generic;

            public class NotAConfig
            {
                public List<List<string>> Nested { get; set; } = new();
            }
            """);
    }

    [Fact]
    public async Task IgnoredProperty_IsNotReported()
    {
        await VerifyAsync("""
            using System.Collections.Generic;
            using Shoko.Abstractions.Config;

            public class MyConfig : IConfiguration
            {
                [System.Text.Json.Serialization.JsonIgnore]
                public List<List<string>> Nested { get; set; } = new();
            }
            """);
    }

    [Fact]
    public async Task SectionReachedFromAConfiguration_IsReported()
    {
        await VerifyAsync("""
            using System.Collections.Generic;
            using Shoko.Abstractions.Config;

            public class Section
            {
                public {|#0:List<List<string>>|} Nested { get; set; } = new();
            }

            public class MyConfig : IConfiguration
            {
                public Section Section { get; set; } = new();
            }
            """,
            new DiagnosticResult(Diagnostics.NestedCollection)
                .WithLocation(0)
                .WithArguments("Nested", "List<List<string>>", "list"));
    }

    [Fact]
    public async Task UnusableDictionaryKey_IsReported()
    {
        await VerifyAsync("""
            using System.Collections.Generic;
            using Shoko.Abstractions.Config;

            public class MyKey
            {
                public string Value { get; set; } = "";
            }

            public class MyConfig : IConfiguration
            {
                public {|#0:Dictionary<MyKey, string>|} Keyed { get; set; } = new();
            }
            """,
            new DiagnosticResult(Diagnostics.UnusableDictionaryKey)
                .WithLocation(0)
                .WithArguments("Keyed", "MyKey"));
    }

    [Fact]
    public async Task UsableDictionaryKeys_AreNotReported()
    {
        await VerifyAsync("""
            using System;
            using System.Collections.Generic;
            using Shoko.Abstractions.Config;

            public enum Colour { Red, Green }

            [Serializable]
            public class MarkedKey
            {
                public string Value { get; set; } = "";
            }

            public class MyConfig : IConfiguration
            {
                public Dictionary<string, int> ByString { get; set; } = new();
                public Dictionary<Colour, int> ByEnum { get; set; } = new();
                public Dictionary<Guid, bool> ByGuid { get; set; } = new();
                public Dictionary<int, string> ByInt { get; set; } = new();
                public Dictionary<MarkedKey, string> ByMarked { get; set; } = new();
            }
            """);
    }

    [Theory]
    [InlineData("ComplexDropdown", "Dropdown")]
    [InlineData("ComplexTab", "Tab")]
    [InlineData("ComplexInline", "Inline")]
    public async Task ComplexListTypeOnScalarElements_IsReported(string listType, string noun)
    {
        await VerifyAsync($$"""
            using System.Collections.Generic;
            using Shoko.Abstractions.Config;
            using Shoko.Abstractions.UI.Attributes;
            using Shoko.Abstractions.UI.Enums;

            public class MyConfig : IConfiguration
            {
                [{|#0:List(ListType = DisplayListType.{{listType}})|}]
                public List<string> Names { get; set; } = new();
            }
            """,
            new DiagnosticResult(Diagnostics.IncompatibleListType)
                .WithLocation(0)
                .WithArguments("Names", noun, "class", listType, "string"));
    }

    /// <summary>
    /// A class the generator refuses to register as a section container, because everything under
    /// the System namespace is excluded.
    /// </summary>
    [Fact]
    public async Task ComplexListTypeOnAFrameworkClass_IsReported()
    {
        await VerifyAsync("""
            using System;
            using System.Collections.Generic;
            using Shoko.Abstractions.Config;
            using Shoko.Abstractions.UI.Attributes;
            using Shoko.Abstractions.UI.Enums;

            public class MyConfig : IConfiguration
            {
                [{|#0:List(ListType = DisplayListType.ComplexTab)|}]
                public List<Uri> Links { get; set; } = new();
            }
            """,
            new DiagnosticResult(Diagnostics.IncompatibleListType)
                .WithLocation(0)
                .WithArguments("Links", "Tab", "class", "ComplexTab", "Uri"));
    }

    [Fact]
    public async Task EnumCheckboxOnNonEnumElements_IsReported()
    {
        await VerifyAsync("""
            using System.Collections.Generic;
            using Shoko.Abstractions.Config;
            using Shoko.Abstractions.UI.Attributes;
            using Shoko.Abstractions.UI.Enums;

            public class MyConfig : IConfiguration
            {
                [{|#0:List(ListType = DisplayListType.EnumCheckbox)|}]
                public List<string> Names { get; set; } = new();
            }
            """,
            new DiagnosticResult(Diagnostics.IncompatibleListType)
                .WithLocation(0)
                .WithArguments("Names", "Checkbox", "enum", "EnumCheckbox", "string"));
    }

    [Theory]
    [InlineData("ComplexDropdown", "Dropdown")]
    [InlineData("ComplexTab", "Tab")]
    [InlineData("ComplexInline", "Inline")]
    public async Task ComplexListTypeWithoutAPrimaryKey_IsReported(string listType, string noun)
    {
        await VerifyAsync($$"""
            using System.Collections.Generic;
            using Shoko.Abstractions.Config;
            using Shoko.Abstractions.UI.Attributes;
            using Shoko.Abstractions.UI.Enums;

            public class Section
            {
                public string Name { get; set; } = "";
            }

            public class MyConfig : IConfiguration
            {
                [{|#0:List(ListType = DisplayListType.{{listType}})|}]
                public List<Section> Sections { get; set; } = new();
            }
            """,
            new DiagnosticResult(Diagnostics.MissingPrimaryKey)
                .WithLocation(0)
                .WithArguments("Sections", noun, "Section"));
    }

    /// <summary>
    /// The schema flattens inheritance and the generator resolves an inherited key through
    /// the flattened property set, so a base-declared [Key] satisfies the requirement.
    /// </summary>
    [Fact]
    public async Task ComplexListTypeWithAnInheritedPrimaryKey_IsNotReported()
    {
        await VerifyAsync("""
            using System.Collections.Generic;
            using System.ComponentModel.DataAnnotations;
            using Shoko.Abstractions.Config;
            using Shoko.Abstractions.UI.Attributes;
            using Shoko.Abstractions.UI.Enums;

            public class SectionBase
            {
                [Key]
                public string Id { get; set; } = "";
            }

            public class Section : SectionBase
            {
                public string Name { get; set; } = "";
            }

            public class MyConfig : IConfiguration
            {
                [List(ListType = DisplayListType.ComplexDropdown)]
                public List<Section> Sections { get; set; } = new();
            }
            """);
    }
    [Fact]
    public async Task ComplexListTypeWithAnIgnoredPrimaryKey_IsReported()
    {
        await VerifyAsync("""
            using System.Collections.Generic;
            using System.ComponentModel.DataAnnotations;
            using Shoko.Abstractions.Config;
            using Shoko.Abstractions.UI.Attributes;
            using Shoko.Abstractions.UI.Enums;

            public class Section
            {
                [Key]
                [System.Text.Json.Serialization.JsonIgnore]
                public string Id { get; set; } = "";

                public string Name { get; set; } = "";
            }

            public class MyConfig : IConfiguration
            {
                [{|#0:List(ListType = DisplayListType.ComplexInline)|}]
                public List<Section> Sections { get; set; } = new();
            }
            """,
            new DiagnosticResult(Diagnostics.MissingPrimaryKey)
                .WithLocation(0)
                .WithArguments("Sections", "Inline", "Section"));
    }

    [Fact]
    public async Task MatchingListTypes_AreNotReported()
    {
        await VerifyAsync("""
            using System.Collections.Generic;
            using System.ComponentModel.DataAnnotations;
            using Shoko.Abstractions.Config;
            using Shoko.Abstractions.UI.Attributes;
            using Shoko.Abstractions.UI.Enums;

            public enum Colour { Red, Green }

            public class Keyed
            {
                [Key]
                public string Id { get; set; } = "";

                public string Name { get; set; } = "";
            }

            public class Section
            {
                public string Name { get; set; } = "";
            }

            public class MyConfig : IConfiguration
            {
                [List(ListType = DisplayListType.EnumCheckbox)]
                public List<Colour> Colours { get; set; } = new();

                // The item type declares the key.
                [List(ListType = DisplayListType.ComplexDropdown)]
                public List<Keyed> Keyed { get; set; } = new();

                // The property itself declares the key.
                [Key]
                [List(ListType = DisplayListType.ComplexTab)]
                public List<Section> Sections { get; set; } = new();

                [List(ListType = DisplayListType.Auto)]
                public List<Section> Auto { get; set; } = new();

                [List(ListType = DisplayListType.Auto)]
                public List<string> Names { get; set; } = new();

                public List<Section> Unattributed { get; set; } = new();
            }
            """);
    }

    [Fact]
    public async Task NonGenericDictionary_IsReported()
    {
        await VerifyAsync("""
            using System.Collections;
            using Shoko.Abstractions.Config;

            public class MyConfig : IConfiguration
            {
                public {|#0:Hashtable|} Table { get; set; } = new();
            }
            """,
            new DiagnosticResult(Diagnostics.NotAGenericDictionary)
                .WithLocation(0)
                .WithArguments("Table", "Hashtable"));
    }

    [Fact]
    public async Task GenericDictionaryImplementations_AreNotReported()
    {
        await VerifyAsync("""
            using System.Collections.Generic;
            using System.Collections.Concurrent;
            using Shoko.Abstractions.Config;

            public class MyConfig : IConfiguration
            {
                public SortedList<string, int> Sorted { get; set; } = new();
                public SortedDictionary<string, int> SortedDict { get; set; } = new();
                public ConcurrentDictionary<string, int> Concurrent { get; set; } = new();
                public IReadOnlyDictionary<string, int> ReadOnly { get; set; } = new Dictionary<string, int>();
                public IDictionary<string, int> Interface { get; set; } = new Dictionary<string, int>();
            }
            """);
    }

    [Fact]
    public async Task ActionParameter_IsReported()
    {
        // An action's invocation parameters are its own settable, serialized
        // properties, walked by the same generator, so the same shape breaks it
        // identically.
        await VerifyAsync("""
            using System.Collections.Generic;
            using Shoko.Abstractions.Actions;

            public class MyAction : IExecutableAction
            {
                public {|#0:List<List<string>>|} Nested { get; set; } = new();
            }
            """,
            new DiagnosticResult(Diagnostics.NestedCollection)
                .WithLocation(0)
                .WithArguments("Nested", "List<List<string>>", "list"));
    }

    [Fact]
    public async Task TypeReachedFromAnAction_IsReported()
    {
        await VerifyAsync("""
            using System.Collections;
            using Shoko.Abstractions.Actions;

            public class Parameters
            {
                public {|#0:Hashtable|} Table { get; set; } = new();
            }

            public class MyAction : IExecutableAction
            {
                public Parameters Parameters { get; set; } = new();
            }
            """,
            new DiagnosticResult(Diagnostics.NotAGenericDictionary)
                .WithLocation(0)
                .WithArguments("Table", "Hashtable"));
    }

    [Fact]
    public async Task ActionMetadataSurface_IsNotReported()
    {
        // Every metadata member is a scalar, so none of the rules can fire on
        // one. The index deliberately does not filter them out.
        await VerifyAsync("""
            using Shoko.Abstractions.Actions;

            public class MyAction : IExecutableAction
            {
                public string Name => "Do The Thing";
                public string? Description => null;
                public bool RequiresConfirmation => true;
            }
            """);
    }

    [Fact]
    public async Task ATypeImplementingNeitherContract_IsNotAnalysed()
    {
        await VerifyAsync("""
            using System.Collections.Generic;

            public class NotAnything
            {
                public List<List<string>> Nested { get; set; } = new();
            }
            """);
    }
    [Fact]
    public async Task EmptinessOperatorWithAValue_IsReported()
    {
        await VerifyAsync("""
            #nullable enable
            using Shoko.Abstractions.Config;
            using Shoko.Abstractions.UI.Attributes;
            using Shoko.Abstractions.UI.Enums;

            public class MyConfig : IConfiguration
            {
                public string Path { get; set; } = "";

                [{|#0:Visibility(DisableWhenMemberIsSet = nameof(Path), DisableOperator = UiConditionOperator.IsEmpty, DisableWhenSetTo = "")|}]
                public string Guarded { get; set; } = "";
            }
            """,
            new DiagnosticResult(Diagnostics.UnusableCondition)
                .WithLocation(0)
                .WithArguments("MyConfig.Guarded", "uses 'IsEmpty', which compares nothing, so it takes no value"));
    }

    [Fact]
    public async Task SetOperatorWithoutValues_IsReported()
    {
        await VerifyAsync("""
            #nullable enable
            using Shoko.Abstractions.Config;
            using Shoko.Abstractions.UI.Attributes;
            using Shoko.Abstractions.UI.Enums;

            public class MyConfig : IConfiguration
            {
                public string Mode { get; set; } = "";

                [{|#0:Visibility(ToggleWhenMemberIsSet = nameof(Mode), ToggleOperator = UiConditionOperator.In)|}]
                public string Guarded { get; set; } = "";
            }
            """,
            new DiagnosticResult(Diagnostics.UnusableCondition)
                .WithLocation(0)
                .WithArguments("MyConfig.Guarded", "uses 'In', which matches against a set, so it needs one or more values"));
    }

    [Fact]
    public async Task APathThroughACollection_IsReported()
    {
        await VerifyAsync("""
            #nullable enable
            using System.Collections.Generic;
            using Shoko.Abstractions.Config;
            using Shoko.Abstractions.UI.Attributes;

            public class Nested { public string Mode { get; set; } = ""; }

            public class MyConfig : IConfiguration
            {
                public List<Nested> Items { get; set; } = new();

                [{|#0:Visibility(DisableWhenMemberIsSet = "Items.Mode", DisableWhenSetTo = "local")|}]
                public string Guarded { get; set; } = "";
            }
            """,
            new DiagnosticResult(Diagnostics.UnusableCondition)
                .WithLocation(0)
                .WithArguments("MyConfig.Guarded", "points through 'Items', which is a collection, and a condition has no index to say which entry it meant"));
    }

    [Fact]
    public async Task AnActionNamingAMemberThatIsNotThere_IsReported()
    {
        await VerifyAsync("""
            #nullable enable
            using Shoko.Abstractions.Config;
            using Shoko.Abstractions.UI.Attributes;

            public class MyConfig : IConfiguration
            {
                [{|#0:CustomAction(DisableWhenMemberIsSet = "Nonexistent", DisableWhenSetTo = true)|}]
                public void DoTheThing() { }
            }
            """,
            new DiagnosticResult(Diagnostics.UnusableCondition)
                .WithLocation(0)
                .WithArguments("MyConfig.DoTheThing", "names 'Nonexistent', which MyConfig does not have"));
    }

    [Fact]
    public async Task ConditionsThatCanHold_AreNotReported()
    {
        await VerifyAsync("""
            #nullable enable
            using Shoko.Abstractions.Config;
            using Shoko.Abstractions.UI.Attributes;
            using Shoko.Abstractions.UI.Enums;

            public class Nested { public int Depth { get; set; } }

            public class MyConfig : IConfiguration
            {
                public string Path { get; set; } = "";
                public string Mode { get; set; } = "";
                public Nested Nested { get; set; } = new();

                [Visibility(DisableWhenMemberIsSet = nameof(Path), DisableOperator = UiConditionOperator.IsEmpty)]
                public string Encoder { get; set; } = "";

                [Visibility(ToggleWhenMemberIsSet = nameof(Mode), ToggleOperator = UiConditionOperator.In, ToggleWhenSetToAny = new object?[] { "vaapi", "qsv" })]
                public string Device { get; set; } = "";

                [Visibility(DisableWhenMemberIsSet = $"{nameof(Nested)}.{nameof(Nested.Depth)}", DisableOperator = UiConditionOperator.GreaterThan, DisableWhenSetTo = 3)]
                public string Deep { get; set; } = "";

                [Visibility(DisableWhenMemberIsSet = nameof(Path), DisableOperator = UiConditionOperator.Contains, DisableWhenSetTo = "ffmpeg")]
                public string Contained { get; set; } = "";
            }
            """);
    }

    [Fact]
    public async Task AHandlerWatchingAMemberThatIsNotThere_IsReported()
    {
        await VerifyAsync("""
            #nullable enable
            using Shoko.Abstractions.Config;
            using Shoko.Abstractions.Config.Attributes;
            using Shoko.Abstractions.Config.Enums;

            public class MyConfig : IConfiguration
            {
                public string Path { get; set; } = "";

                [{|#0:ConfigurationAction(ConfigurationActionType.LiveEdit, ReactiveMembers = new[] { "Nonexistent" })|}]
                public void OnEdit() { }
            }
            """,
            new DiagnosticResult(Diagnostics.UnusableReactiveHandler)
                .WithLocation(0)
                .WithArguments("MyConfig.OnEdit", "watches a member that names 'Nonexistent', which MyConfig does not have"));
    }

    [Fact]
    public async Task AHandlerWatchingThroughACollection_IsReported()
    {
        await VerifyAsync("""
            #nullable enable
            using System.Collections.Generic;
            using Shoko.Abstractions.Config;
            using Shoko.Abstractions.Config.Attributes;
            using Shoko.Abstractions.Config.Enums;

            public class Nested { public string Mode { get; set; } = ""; }

            public class MyConfig : IConfiguration
            {
                public List<Nested> Items { get; set; } = new();

                [{|#0:ConfigurationAction(ConfigurationActionType.LiveEdit, ReactiveMembers = new[] { "Items.Mode" })|}]
                public void OnEdit() { }
            }
            """,
            new DiagnosticResult(Diagnostics.UnusableReactiveHandler)
                .WithLocation(0)
                .WithArguments("MyConfig.OnEdit", "watches a member that points through 'Items', which is a collection, and a condition has no index to say which entry it meant"));
    }

    [Fact]
    public async Task AHookNoEventRaisesNarrowingItself_IsReported()
    {
        await VerifyAsync("""
            #nullable enable
            using Shoko.Abstractions.Config;
            using Shoko.Abstractions.Config.Attributes;
            using Shoko.Abstractions.Config.Enums;

            public class MyConfig : IConfiguration
            {
                public string Path { get; set; } = "";

                [{|#0:ConfigurationAction(ConfigurationActionType.Save, ReactiveMembers = new[] { nameof(Path) })|}]
                public void OnSave() { }
            }
            """,
            new DiagnosticResult(Diagnostics.UnusableReactiveHandler)
                .WithLocation(0)
                .WithArguments("MyConfig.OnSave", "handles a hook that no event raises, so it cannot narrow what it reacts to"));
    }

    [Fact]
    public async Task AHandlerWatchingWhatIsThere_IsNotReported()
    {
        await VerifyAsync("""
            #nullable enable
            using Shoko.Abstractions.Config;
            using Shoko.Abstractions.Config.Attributes;
            using Shoko.Abstractions.Config.Enums;

            public class Nested { public string Mode { get; set; } = ""; }

            public class MyConfig : IConfiguration
            {
                public string Path { get; set; } = "";
                public Nested Nested { get; set; } = new();

                [ConfigurationAction(ConfigurationActionType.LiveEdit,
                    Events = new[] { ReactiveEventType.Unfocused, ReactiveEventType.Edited },
                    ReactiveMembers = new[] { nameof(Path), $"{nameof(Nested)}.{nameof(Nested.Mode)}" })]
                public void OnEdit() { }

                [ConfigurationAction(ConfigurationActionType.Save)]
                public void OnSave() { }
            }
            """);
    }

}
