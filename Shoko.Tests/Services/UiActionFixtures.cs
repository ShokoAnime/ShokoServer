using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Tests.Services;

/// <summary>
///   A global action with a realistic parameter set.
/// </summary>
/// <remarks>
///   No in-tree action declares parameters today, so the fixtures in this file
///   are the only coverage the parameter description has. Every member of the
///   metadata surface is declared explicitly rather than left to the interface
///   defaults, so the exclusion rule is exercised against real properties on
///   the concrete type.
/// </remarks>
[Section(DisplaySectionType.FieldSet, DefaultSectionName = "General")]
public class ParameterisedGlobalAction : IExecutableAction
{
    /// <inheritdoc />
    public string Name => "Reindex Library";

    /// <inheritdoc />
    public string? Description => "Rebuilds the search index.";

    /// <inheritdoc />
    public ActionCategory Category => ActionCategory.Maintenance;

    /// <inheritdoc />
    public ActionPermission Permission => ActionPermission.Admin;

    /// <inheritdoc />
    public bool RequiresConfirmation => true;

    /// <summary>The text to match against.</summary>
    [Display(Name = "Search Query", Order = 1)]
    [Badge("New", Theme = DisplayColorTheme.Primary)]
    [DefaultValue("")]
    public string Query { get; set; } = string.Empty;

    /// <summary>How thorough to be.</summary>
    [Display(Order = 2)]
    [SectionName("Behaviour")]
    public TwinMode Mode { get; set; } = TwinMode.Balanced;

    /// <summary>How many entries to touch at most.</summary>
    [Display(Order = 3)]
    [Range(1, 100)]
    [Visibility(Size = DisplayElementSize.Small, Advanced = true, ToggleWhenMemberIsSet = nameof(DryRun), ToggleWhenSetTo = true, ToggleVisibilityTo = DisplayVisibility.ReadOnly)]
    public int MaxResults { get; set; } = 25;

    /// <summary>The tags to restrict the run to.</summary>
    [Display(Order = 4)]
    [List(UniqueItems = true)]
    public List<string> Tags { get; set; } = [];

    /// <summary>Whether to report instead of write.</summary>
    [Display(Order = 5)]
    public bool DryRun { get; set; }

    /// <inheritdoc />
    public Task Execute(CancellationToken token = default)
        => Task.CompletedTask;
}

/// <summary>
///   A global action that takes no parameters at all — the common case.
/// </summary>
public class ParameterlessGlobalAction : IExecutableAction
{
    /// <inheritdoc />
    public string Name => "Run Import";

    /// <inheritdoc />
    public string? Description => "Sweeps every managed folder.";

    /// <inheritdoc />
    public ActionCategory Category => ActionCategory.Maintenance;

    /// <inheritdoc />
    public ActionPermission Permission => ActionPermission.Admin;

    /// <inheritdoc />
    public bool RequiresConfirmation => false;

    /// <inheritdoc />
    public Task Execute(CancellationToken token = default)
        => Task.CompletedTask;
}

/// <summary>
///   An action whose parameters cannot be described: a collection inside a
///   collection has no renderable form and no distinct schema key.
/// </summary>
public class UndescribableGlobalAction : IExecutableAction
{
    /// <summary>The offending parameter.</summary>
    public List<List<string>> Nested { get; set; } = [];

    /// <inheritdoc />
    public string Name => "Undescribable";

    /// <inheritdoc />
    public string? Description => null;

    /// <inheritdoc />
    public ActionCategory Category => ActionCategory.Maintenance;

    /// <inheritdoc />
    public ActionPermission Permission => ActionPermission.Admin;

    /// <inheritdoc />
    public bool RequiresConfirmation => false;

    /// <inheritdoc />
    public Task Execute(CancellationToken token = default)
        => Task.CompletedTask;
}

/// <summary>
///   A scoped action, which adds <c>Scope</c> and a protected entity context on
///   top of what <see cref="IExecutableAction"/> declares.
/// </summary>
public class ParameterisedSeriesAction : SeriesAction
{
    /// <inheritdoc />
    public override string Name => "Rescan Series";

    /// <inheritdoc />
    public override ActionPermission Permission => ActionPermission.User;

    /// <summary>Whether to go past the cache.</summary>
    [Display(Order = 1)]
    public bool Force { get; set; }

    /// <summary>
    ///   Reads the context the framework populates, so the property cannot be
    ///   optimised away and genuinely exists on the walked type.
    /// </summary>
    public override Task Execute(CancellationToken token = default)
        => Task.FromResult(Series);
}

/// <summary>
///   An action whose parameter merely shares a name with a metadata member.
/// </summary>
/// <remarks>
///   <see cref="IExecutableAction.Name"/> is implemented explicitly, which
///   frees the public <c>Name</c> to be an ordinary parameter of a different
///   type. It is here to pin that the exclusion rule matches on name
///   <em>and</em> type rather than on name alone.
/// </remarks>
public class ShadowedNameAction : IExecutableAction
{
    string IExecutableAction.Name => "Shadowed";

    /// <inheritdoc />
    public ActionPermission Permission => ActionPermission.Admin;

    /// <summary>An ordinary parameter that happens to be called Name.</summary>
    public int Name { get; set; }

    /// <inheritdoc />
    public Task Execute(CancellationToken token = default)
        => Task.CompletedTask;
}

/// <summary>
///   An action that declares its metadata as ordinary settable properties.
/// </summary>
/// <remarks>
///   Nothing stops a plugin author writing an action this way — the interface
///   only asks for a getter. It exists to prove that the metadata surface is
///   hidden from population as well as from the schema, rather than merely
///   being unwritable because the in-tree actions all happen to use
///   expression-bodied getters.
/// </remarks>
public class SettableMetadataAction : IExecutableAction
{
    /// <inheritdoc />
    public string Name { get; set; } = "Settable Metadata";

    /// <inheritdoc />
    public string? Description { get; set; } = "Not a parameter.";

    /// <inheritdoc />
    public ActionCategory Category { get; set; } = ActionCategory.Maintenance;

    /// <inheritdoc />
    public ActionPermission Permission { get; set; } = ActionPermission.Admin;

    /// <inheritdoc />
    public bool RequiresConfirmation { get; set; } = true;

    /// <summary>The one genuine parameter.</summary>
    public bool Force { get; set; }

    /// <inheritdoc />
    public Task Execute(CancellationToken token = default)
        => Task.CompletedTask;
}

/// <summary>
///   An action whose parameters include a nested class and a dictionary.
/// </summary>
/// <remarks>
///   Closing an object against unknown properties has to reach the nested
///   class too — a typo one level down is just as silent — while leaving a
///   dictionary alone, since a dictionary carries its value type in
///   <c>additionalProperties</c> and closing it would reject every entry.
/// </remarks>
public class NestedParameterAction : IExecutableAction
{
    /// <inheritdoc />
    public string Name => "Nested Parameters";

    /// <inheritdoc />
    public ActionPermission Permission => ActionPermission.Admin;

    /// <summary>A nested parameter object.</summary>
    public NestedParameterOptions Options { get; set; } = new();

    /// <summary>Per-name overrides.</summary>
    public Dictionary<string, int> Weights { get; set; } = [];

    /// <inheritdoc />
    public Task Execute(CancellationToken token = default)
        => Task.CompletedTask;
}

/// <summary>
///   The nested half of <see cref="NestedParameterAction"/>.
/// </summary>
public class NestedParameterOptions
{
    /// <summary>Whether to go past the cache.</summary>
    public bool Force { get; set; }

    /// <summary>How deep to go.</summary>
    public int Depth { get; set; } = 1;
}
