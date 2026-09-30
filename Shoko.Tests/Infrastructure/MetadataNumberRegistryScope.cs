using System;
using System.IO;
using System.Reflection;
using Shoko.Server.Databases;
using Xunit;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// Points <see cref="MetadataNumberRegistry"/> at a temporary data folder, and
/// lets go of it again on dispose.
/// </summary>
/// <remarks>
/// The registry is static and process-global, so every test using this type
/// must join <see cref="MetadataNumberRegistryCollection"/>. On dispose the
/// registry keeps its numbers in memory but stops writing them to disk, as it
/// does before the server ever loads it.
/// </remarks>
public sealed class MetadataNumberRegistryScope : IDisposable
{
    private static readonly FieldInfo _filePathField = typeof(MetadataNumberRegistry)
        .GetField("_filePath", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly FieldInfo _unreadableField = typeof(MetadataNumberRegistry)
        .GetField("_unreadable", BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Creates the temporary data folder, optionally with a registry file
    /// already in it.
    /// </summary>
    /// <param name="fileContents">The registry file to start with, if any.</param>
    /// <param name="load">Whether to load the registry from the folder right away.</param>
    public MetadataNumberRegistryScope(string? fileContents = null, bool load = true)
    {
        DataPath = Path.Join(Path.GetTempPath(), $"shoko-metadata-number-registry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(DataPath);
        if (fileContents is not null)
            File.WriteAllText(FilePath, fileContents);
        if (load)
            Load();
    }

    /// <summary>
    /// The temporary data folder.
    /// </summary>
    public string DataPath { get; }

    /// <summary>
    /// The registry file in the temporary data folder.
    /// </summary>
    public string FilePath => Path.Join(DataPath, MetadataNumberRegistry.FileName);

    /// <summary>
    /// Loads the registry from the temporary data folder.
    /// </summary>
    public void Load()
        => MetadataNumberRegistry.Load(DataPath);

    /// <summary>
    /// Stops the registry from writing to disk, as if it was never loaded,
    /// and forgets any unreadable file the last load found.
    /// </summary>
    public static void Unload()
    {
        _filePathField.SetValue(null, null);
        _unreadableField.SetValue(null, false);
    }

    public void Dispose()
    {
        Unload();
        try
        {
            Directory.Delete(DataPath, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

/// <summary>
/// Serialises every test that uses <see cref="MetadataNumberRegistry"/>'s
/// static state. Tests outside this collection keep running in parallel.
/// </summary>
[CollectionDefinition(nameof(MetadataNumberRegistryCollection), DisableParallelization = true)]
public sealed class MetadataNumberRegistryCollection;
