using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Actions;

/// <summary>
///   Delete the group, and optionally the series in it and their files.
/// </summary>
public sealed class DeleteGroupAction(IShokoGroupManager groupManagementService) : GroupAction
{
    /// <summary>
    ///   Delete the series in the group as well. A group that still holds
    ///   series cannot be deleted without this.
    /// </summary>
    public bool DeleteSeries { get; set; }

    /// <summary>
    ///   Delete the files of those series from disk as well. Only meaningful
    ///   together with <see cref="DeleteSeries"/>.
    /// </summary>
    public bool DeleteFiles { get; set; }

    public override string Name => "Delete Group";

    public override string? Description => "Deletes the group, and optionally the series in it and their files.";

    public override ActionCategory Category => ActionCategory.Destructive;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override bool RequiresConfirmation => true;

    public override string? ConfirmationMessage => DeleteFiles
        ? "This will delete the group, every series in it, and their files from disk. This cannot be undone."
        : DeleteSeries
            ? "This will delete the group and every series in it. The files are left on disk."
            : "This will delete the group. It must be empty.";

    public override Task<ActionValidationResult?> Validate(CancellationToken token = default)
        => Task.FromResult(!DeleteSeries && ((AnimeGroup)Group).AllSeries.Any()
            ? new ActionValidationResult("The group still contains series. Move them, or set DeleteSeries.")
            : null);

    public override Task Execute(CancellationToken token = default)
        => groupManagementService.DeleteGroup((AnimeGroup)Group, DeleteSeries, DeleteFiles);
}
