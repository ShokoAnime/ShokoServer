using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Actions;

/// <summary>
///   Delete the file, and optionally its locations on disk.
/// </summary>
public sealed class DeleteVideoFileAction(IVideoService videoService) : VideoAction
{
    /// <summary>
    ///   Remove the physical locations from disk as well as the record.
    /// </summary>
    public bool RemoveFiles { get; set; } = true;

    /// <summary>
    ///   Remove the containing folder if deleting the file leaves it empty.
    ///   Skipping this is markedly faster when deleting many files from one
    ///   folder, since the check runs per file.
    /// </summary>
    public bool RemoveFolder { get; set; } = true;

    public override string Name => "Delete File";

    public override string? Description => "Deletes the file, and optionally its locations on disk.";

    public override ActionCategory Category => ActionCategory.Destructive;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override bool RequiresConfirmation => true;

    public override string? ConfirmationMessage => RemoveFiles
        ? "This will delete the file from disk. This cannot be undone."
        : "This will remove the file from the collection. The file itself is left on disk.";

    public override Task Execute(CancellationToken token = default)
        => videoService.DeleteVideo((VideoLocal)Video, RemoveFiles, RemoveFolder);
}
