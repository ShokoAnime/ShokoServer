namespace Shoko.Plugin.Abstractions;

public interface IBaseRenamer
{
    /// <summary>
    /// The human-readable name of the renamer
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The description of the renamer
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// This should be true if the renamer supports moving.
    /// If it is set to false, it will use an <see cref="IFallbackRenamer"/> with even attempting to use the result from <see cref="IRenamer.GetNewPath"/> or <see cref="IRenamer{T}.GetNewPath"/>
    /// </summary>
    bool SupportsMoving { get; }

    /// <summary>
    /// This should be true if the renamer supports renaming.
    /// If it is set to false, it will use an <see cref="IFallbackRenamer"/> with even attempting to use the result from <see cref="IRenamer.GetNewPath"/> or <see cref="IRenamer{T}.GetNewPath"/>
    /// </summary>
    bool SupportsRenaming { get; }
}

/// <summary>
/// A renamer that knows how to operate on recognized files.
/// </summary>
public interface IRenamer : IBaseRenamer
{
    /// <summary>
    /// Get the new path for moving and/or renaming. See <see cref="MoveRenameResult"/> and its <see cref="MoveRenameResult.Error"/> for details on the return value.
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    MoveRenameResult GetNewPath(MoveRenameEventArgs args);
}
/// <summary>
/// A renamer with a settings model. <see cref="T"/> is the type of the settings model.
/// </summary>
/// <typeparam name="T">Type of the settings model</typeparam>
public interface IRenamer<T> : IBaseRenamer where T : class
{
    /// <summary>
    /// Get the new path for moving and/or renaming. See <see cref="MoveRenameResult"/> and its <see cref="MoveRenameResult.Error"/> for details on the return value.
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    MoveRenameResult GetNewPath(MoveRenameEventArgs<T> args);
}

/// <summary>
/// A renamer that knows how to operate on both recognized and unrecognized files.
/// </summary>
public interface IUnrecognizedRenamer : IRenamer;

/// <summary>
/// A renamer that knows how to operate on both recognized and unrecognized files.
/// This version also takes in a settings model. <see cref="T"/> is the type of the settings model.
/// </summary>
public interface IUnrecognizedRenamer<T> : IRenamer<T> where T : class;

/// <summary>
/// A renamer that should be used if the primary renamer does not support the operation.
/// The Legacy/WebAOM renamer does not explicitly implement this, but will be used as a fallback if another is not provided and the primary renamer does not support the operation.
/// </summary>
public interface IFallbackRenamer : IRenamer;

/// <summary>
/// A renamer that should be used if the primary renamer does not support the operation.
/// The Legacy/WebAOM renamer does not explicitly implement this, but will be used as a fallback if another is not provided and the primary renamer does not support the operation.
/// This version also takes in a settings model. <see cref="T"/> is the type of the settings model.
/// </summary>
public interface IFallbackRenamer<T> : IRenamer<T> where T : class;
