using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions;

/// <summary>
/// A renamer that knows how to operate on recognized files.
/// </summary>
public interface IRenamer
{
    string GetFilename(RenameEventArgs args);

    (IImportFolder destination, string subfolder) GetDestination(MoveEventArgs args);
}

/// <summary>
/// A renamer that knows how to operate on both recognized and unrecognized files.
/// </summary>
public interface IUnrecognizedRenamer : IRenamer { }
