using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Exceptions;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Settings;

/// <summary>
/// Drives the real <see cref="ConfigurationService"/> over the real
/// <see cref="ServerSettings"/> to show that the credentials the server ships
/// with never reach the outward-facing serialization, that a plugin reading the
/// same configuration still gets them, and that handing the masked document back
/// does not destroy them.
/// </summary>
public sealed class ConfigurationSecretApiSurfaceTests : IDisposable
{
    #region Fixture

    private const string AnidbPassword = "anidb-password-a1b2c3";

    private const string AvdumpKey = "avdump-key-d4e5f6";

    private const string DatabasePassword = "database-password-g7h8i9";

    private const string TmdbApiKey = "tmdb-api-key-j1k2l3";

    private const string PlexToken = "plex-token-m4n5o6";

    /// <summary>
    /// Every credential this fixture writes into the settings. A masked document
    /// must not contain any of them anywhere.
    /// </summary>
    private static readonly string[] _secrets = [AnidbPassword, AvdumpKey, DatabasePassword, TmdbApiKey];

    private readonly string _dataPath = Path.Join(Path.GetTempPath(), $"shoko-secret-tests-{Guid.NewGuid():N}");

    private readonly ConfigurationService _service;

    private readonly ConfigurationInfo _info;

    public ConfigurationSecretApiSurfaceTests()
    {
        Directory.CreateDirectory(_dataPath);

        var applicationPaths = new Mock<IApplicationPaths>(MockBehavior.Loose);
        applicationPaths.SetupGet(paths => paths.DataPath).Returns(_dataPath);
        applicationPaths.SetupGet(paths => paths.ConfigurationsPath).Returns(Path.Join(_dataPath, "configurations"));

        _service = new ConfigurationService(NullLoggerFactory.Instance, applicationPaths.Object, new Mock<IPluginManager>(MockBehavior.Loose).Object);
        _info = _service.GetConfigurationInfo<ServerSettings>();

        var settings = _service.Load<ServerSettings>();
        settings.AniDb.Password = AnidbPassword;
        settings.AniDb.AVDumpKey = AvdumpKey;
        settings.Database.Password = DatabasePassword;
        settings.TMDB.UserApiKey = TmdbApiKey;
        // Not a marked secret; it is here to prove masking replaces the marked
        // properties rather than every string that looks sensitive.
        settings.Plex.Server = PlexToken;
        _service.Save(settings);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
            Directory.Delete(_dataPath, recursive: true);
    }

    #endregion

    #region The outward-facing surface

    [Fact]
    public void SerializeWithMasking_EmitsNoneOfTheKnownCredentials()
    {
        // Written out and read back rather than inspected in memory, so that what
        // is checked is the byte stream a client would receive.
        var path = Path.Join(_dataPath, "masked-response.json");
        File.WriteAllText(path, _service.SerializeWithMasking(_service.Load<ServerSettings>()));
        var emitted = File.ReadAllText(path);

        foreach (var secret in _secrets)
            Assert.DoesNotContain(secret, emitted, StringComparison.Ordinal);

        Assert.True(ConfigurationSecrets.IsMasked(JObject.Parse(emitted)["AniDb"]!["Password"]!.Value<string>()));
        // A string that merely looks sensitive but carries no marker must still
        // come through untouched; masking follows the attribute, not the name.
        Assert.Contains(PlexToken, emitted, StringComparison.Ordinal);
    }

    [Fact]
    public void SerializeWithMasking_ReplacesEachMarkedPropertyIndividually()
    {
        var masked = JObject.Parse(_service.SerializeWithMasking(_service.Load<ServerSettings>()));

        Assert.True(ConfigurationSecrets.IsMasked(masked["AniDb"]!["Password"]!.Value<string>()));
        Assert.True(ConfigurationSecrets.IsMasked(masked["AniDb"]!["AVDumpKey"]!.Value<string>()));
        Assert.True(ConfigurationSecrets.IsMasked(masked["Database"]!["Password"]!.Value<string>()));
        Assert.True(ConfigurationSecrets.IsMasked(masked["TMDB"]!["UserApiKey"]!.Value<string>()));
    }

    [Fact]
    public void MaskSecrets_CoversTheLegacySettingsEndpointsSerializer()
    {
        // GET /api/v3/Settings serializes with MVC's Newtonsoft settings rather
        // than the configuration service's, so the masking has to work on that
        // document too.
        var serializer = JsonSerializer.Create(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include });
        var json = JObject.FromObject(_service.Load<ServerSettings>(), serializer).ToString(Formatting.None);

        var masked = _service.MaskSecrets(_info, json);

        foreach (var secret in _secrets)
            Assert.DoesNotContain(secret, masked, StringComparison.Ordinal);
    }

    #endregion

    #region The plugin surface

    [Fact]
    public void Load_StillHandsAPluginTheRealCredentials()
    {
        // Plugins act for the server and its users and never go through a
        // serializer, so nothing on this path is masked.
        var settings = _service.Load<ServerSettings>();

        Assert.Equal(AnidbPassword, settings.AniDb.Password);
        Assert.Equal(AvdumpKey, settings.AniDb.AVDumpKey);
        Assert.Equal(DatabasePassword, settings.Database.Password);
        Assert.Equal(TmdbApiKey, settings.TMDB.UserApiKey);
    }

    [Fact]
    public void TheStoredFile_StillHoldsTheRealCredentials()
    {
        var stored = File.ReadAllText(_info.Path!);

        foreach (var secret in _secrets)
            Assert.Contains(secret, stored, StringComparison.Ordinal);
    }

    #endregion

    #region Round trips

    [Fact]
    public void SavingBackAMaskedDocument_LeavesTheStoredCredentialsAlone()
    {
        var masked = _service.SerializeWithMasking(_service.Load<ServerSettings>());

        _service.Save<ServerSettings>(masked);

        var settings = _service.Load<ServerSettings>();
        Assert.Equal(AnidbPassword, settings.AniDb.Password);
        Assert.Equal(AvdumpKey, settings.AniDb.AVDumpKey);
        Assert.Equal(DatabasePassword, settings.Database.Password);
        Assert.Equal(TmdbApiKey, settings.TMDB.UserApiKey);
        Assert.Contains(AnidbPassword, File.ReadAllText(_info.Path!), StringComparison.Ordinal);
    }

    [Fact]
    public void SavingBackAMaskedDocument_StillAppliesEveryOtherChange()
    {
        var masked = JObject.Parse(_service.SerializeWithMasking(_service.Load<ServerSettings>()));
        masked["AniDb"]!["Username"] = "someone-else";

        _service.Save<ServerSettings>(masked.ToString(Formatting.None));

        var settings = _service.Load<ServerSettings>();
        Assert.Equal("someone-else", settings.AniDb.Username);
        Assert.Equal(AnidbPassword, settings.AniDb.Password);
    }

    [Fact]
    public void SavingANewValue_ReplacesTheStoredCredential()
    {
        var masked = JObject.Parse(_service.SerializeWithMasking(_service.Load<ServerSettings>()));
        masked["AniDb"]!["Password"] = "a-brand-new-password";

        _service.Save<ServerSettings>(masked.ToString(Formatting.None));

        Assert.Equal("a-brand-new-password", _service.Load<ServerSettings>().AniDb.Password);
    }

    [Fact]
    public void SavingAnEmptyValue_ClearsTheStoredCredential()
    {
        var masked = JObject.Parse(_service.SerializeWithMasking(_service.Load<ServerSettings>()));
        masked["AniDb"]!["Password"] = string.Empty;

        _service.Save<ServerSettings>(masked.ToString(Formatting.None));

        Assert.Equal(string.Empty, _service.Load<ServerSettings>().AniDb.Password);
        Assert.DoesNotContain(AnidbPassword, File.ReadAllText(_info.Path!), StringComparison.Ordinal);
    }

    [Fact]
    public void SavingADocumentThatOmitsTheSecret_LeavesTheStoredCredentialAlone()
    {
        // A client that simply drops the property, the way a JSON Patch that does
        // not mention it would, must not end up clearing it.
        var masked = JObject.Parse(_service.SerializeWithMasking(_service.Load<ServerSettings>()));
        ((JObject)masked["AniDb"]!).Remove("Password");
        masked["AniDb"]!["Username"] = "some-user";

        _service.Save<ServerSettings>(masked.ToString(Formatting.None));

        var settings = _service.Load<ServerSettings>();
        Assert.Equal("some-user", settings.AniDb.Username);
        Assert.Equal(AnidbPassword, settings.AniDb.Password);
    }

    [Fact]
    public void TheFingerprintKey_BelongsToTheDataDirectory()
    {
        // The key is created on first use, kept out of the settings file, and
        // shared by anything reading the same data directory.
        var first = _service.SerializeWithMasking(_service.Load<ServerSettings>());

        var keyPath = Path.Join(_dataPath, "configuration-secrets.key");
        Assert.Equal(32, File.ReadAllBytes(keyPath).Length);
        Assert.DoesNotContain(Convert.ToBase64String(File.ReadAllBytes(keyPath)), File.ReadAllText(_info.Path!), StringComparison.Ordinal);

        var applicationPaths = new Mock<IApplicationPaths>(MockBehavior.Loose);
        applicationPaths.SetupGet(paths => paths.DataPath).Returns(_dataPath);
        var sameInstall = new ConfigurationService(NullLoggerFactory.Instance, applicationPaths.Object, new Mock<IPluginManager>(MockBehavior.Loose).Object);
        var token = JObject.Parse(first)["AniDb"]!["Password"]!.DeepClone();
        var masked = sameInstall.MaskSecrets(JObject.Parse(_service.Serialize(_service.Load<ServerSettings>())), typeof(ServerSettings));

        Assert.Equal(token.Value<string>(), masked["AniDb"]!["Password"]!.Value<string>());
    }

    [Fact]
    public void SavingTheSentinelWithNothingStored_IsRejectedWithAClearError()
    {
        _service.Save<ServerSettings>(JObject.Parse(_service.SerializeWithMasking(_service.Load<ServerSettings>()))
            .Also(document => document["AniDb"]!["Password"] = string.Empty)
            .ToString(Formatting.None));

        var masked = JObject.Parse(_service.SerializeWithMasking(_service.Load<ServerSettings>()));
        masked["AniDb"]!["Password"] = ConfigurationSecrets.Sentinel;

        var exception = Assert.Throws<ConfigurationValidationException>(() => _service.Save<ServerSettings>(masked.ToString(Formatting.None)));

        var error = Assert.Contains("AniDb.Password", exception.ValidationErrors);
        Assert.Contains(ConfigurationSecrets.Sentinel, Assert.Single(error));
    }

    #endregion
}

file static class JObjectExtensions
{
    internal static JObject Also(this JObject obj, Action<JObject> action)
    {
        action(obj);
        return obj;
    }
}
