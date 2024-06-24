namespace Shoko.Plugin.Abstractions;

/// <summary>
/// A renamer that knows how to operate on recognized files.
/// </summary>
public interface IRenamer<in T> where T : MoveRenameEventArgs
{
    /// <summary>
    /// Get the new path for moving and/or renaming. See <see cref="MoveRenameResult"/> and its <see cref="MoveRenameResult.Error"/> for details on the return value.
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    MoveRenameResult GetNewPath(T args);

    /// <summary>
    /// This should be true if the renamer supports moving.
    /// If it is set to false, it will use an <see cref="IFallbackRenamer"/> with even attempting to use the result from <see cref="GetNewPath"/>
    /// </summary>
    bool SupportsMoving { get; }

    /// <summary>
    /// This should be true if the renamer supports renaming.
    /// If it is set to false, it will use an <see cref="IFallbackRenamer"/> with even attempting to use the result from <see cref="GetNewPath"/>
    /// </summary>
    bool SupportsRenaming { get; }
}

/// <summary>
/// A renamer that knows how to operate on both recognized and unrecognized files.
/// </summary>
public interface IUnrecognizedRenamer : IRenamer<MoveRenameEventArgs>;

/// <summary>
/// A renamer that knows how to operate on both recognized and unrecognized files.
/// This version also takes in a settings model. <see cref="T"/> is the type of the settings model.
/// </summary>
public interface IUnrecognizedRenamer<T> : IUnrecognizedRenamer where T : class;

/// <summary>
/// A renamer that should be used if the primary renamer does not support the operation.
/// The Legacy/WebAOM renamer does not explicitly implement this, but will be used as a fallback if another is not provided and the primary renamer does not support the operation.
/// </summary>
public interface IFallbackRenamer : IRenamer<MoveRenameEventArgs>;

/// <summary>
/// A renamer that should be used if the primary renamer does not support the operation.
/// The Legacy/WebAOM renamer does not explicitly implement this, but will be used as a fallback if another is not provided and the primary renamer does not support the operation.
/// This version also takes in a settings model. <see cref="T"/> is the type of the settings model.
/// </summary>
public interface IFallbackRenamer<T> : IFallbackRenamer where T : class;
