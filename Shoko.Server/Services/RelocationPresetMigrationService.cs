using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MessagePack;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Video.Relocation;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

public class RelocationPresetMigrationService(
    IVideoRelocationService relocationService,
    IRelocationPresetManager presetManager,
    IConfigurationService configurationService,
    ISettingsProvider settingsProvider,
    ILogger<RelocationPresetMigrationService> logger)
{
    public void MigrateFailedPresets()
    {
        var basePath = GetFailedMigrationsBasePath();
        if (!Directory.Exists(basePath))
            return;

        LogExistingErrorFiles(basePath);
        ProcessScripts(Path.Combine(basePath, "RenamerScript"));
        ProcessConfigs(Path.Combine(basePath, "RenamerConfig"));
        CleanupEmptyDirs(basePath);
    }

    private string GetFailedMigrationsBasePath()
    {
        var settings = settingsProvider.GetSettings();
        var dirPath = settings.Database.DatabaseBackupDirectory;
        return Path.Combine(
            string.IsNullOrWhiteSpace(dirPath)
                ? ApplicationPaths.StaticDataPath
                : Path.Combine(ApplicationPaths.StaticDataPath, dirPath),
            "failed_migrations"
        );
    }

    private void LogExistingErrorFiles(string basePath)
    {
        foreach (var errorFile in Directory.GetFiles(basePath, "*.error", SearchOption.AllDirectories))
            logger.LogWarning("Failed migration preset file still pending manual resolution: {Path}", errorFile);
    }

    private void ProcessScripts(string categoryDir)
    {
        if (!Directory.Exists(categoryDir))
            return;

        foreach (var typeDir in Directory.GetDirectories(categoryDir))
        {
            var typeName = Path.GetFileName(typeDir);
            var provider = FindProvider(typeName);
            if (provider is null)
                continue;

            foreach (var file in Directory.GetFiles(typeDir, "*.txt"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                try
                {
                    var script = File.ReadAllText(file, Encoding.UTF8);
                    var config = configurationService.New(provider.ConfigurationInfo!) as IRelocationProviderConfiguration;
                    provider.ConfigurationInfo!.Type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .FirstOrDefault(b => b.Name == "Script")
                        ?.SetValue(config, script);
                    presetManager.StorePreset(provider.Provider, name, config);
                    logger.LogInformation("Re-imported failed migration preset: {File}", file);
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to re-import failed migration preset: {File}", file);
                    var errorFile = Path.ChangeExtension(file, ".error");
                    if (File.Exists(errorFile))
                        File.Delete(errorFile);
                    File.Move(file, errorFile);
                }
            }

            CleanupEmptyDirs(typeDir);
        }
    }

    private void ProcessConfigs(string categoryDir)
    {
        if (!Directory.Exists(categoryDir))
            return;

        foreach (var typeDir in Directory.GetDirectories(categoryDir))
        {
            var typeName = Path.GetFileName(typeDir);
            var provider = FindProvider(typeName);
            if (provider is null)
                continue;

            foreach (var file in Directory.GetFiles(typeDir, "*.messagepack"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                try
                {
                    var bytes = File.ReadAllBytes(file);
                    var config = bytes.Length is 0 ? null : MessagePackSerializer.Typeless.Deserialize(bytes) as IRelocationProviderConfiguration;
                    if (config is null && bytes.Length is > 0)
                    {
                        logger.LogWarning("Failed to re-import failed migration preset: deserialized config is null or wrong type: {File}", file);
                        var errorFile = Path.ChangeExtension(file, ".error");
                        if (File.Exists(errorFile))
                            File.Delete(errorFile);
                        File.Move(file, errorFile);
                        continue;
                    }

                    presetManager.StorePreset(provider.Provider, name, config);
                    logger.LogInformation("Re-imported failed migration preset: {File}", file);
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to re-import failed migration preset: {File}", file);
                    var errorFile = Path.ChangeExtension(file, ".error");
                    if (File.Exists(errorFile))
                        File.Delete(errorFile);
                    File.Move(file, errorFile);
                }
            }

            CleanupEmptyDirs(typeDir);
        }
    }

    private RelocationProviderInfo? FindProvider(string typeName)
        => relocationService.GetAvailableProviders()
            .FirstOrDefault(p => p.Provider.GetType().FullName == typeName);

    private void CleanupEmptyDirs(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            var dir = new DirectoryInfo(path);
            while (dir.Parent is not null && !dir.EnumerateFileSystemInfos().Any())
            {
                var parent = dir.Parent;
                dir.Delete();
                dir = parent;
            }
        }
        catch
        {
            // ignore
        }
    }
}
