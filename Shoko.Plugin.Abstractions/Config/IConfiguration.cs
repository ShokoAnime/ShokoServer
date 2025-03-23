using Shoko.Plugin.Abstractions.Services;

namespace Shoko.Plugin.Abstractions.Config;

/// <summary>
/// Base interface for all configurations served through the
/// <see cref="IConfigurationService"/> or
///  <see cref="ConfigurationProvider{TConfig}"/>.
/// </summary>
public interface IConfiguration { }

/// <summary>
/// Interface for signaling that the configuration should use Newtonsoft.Json
/// for serialization/deserialization instead of System.Text.Json.
/// </summary>
public interface INewtonsoftJsonConfiguration : IConfiguration { }

/// <summary>
/// Interface for signaling that the configuration should be hidden from any UI.
/// </summary>
public interface IHiddenConfiguration : IConfiguration { }

/// <summary>
/// Interface for signaling that the configuration is tied to a hash provider.
/// </summary>
public interface IHashProviderConfiguration : IHiddenConfiguration { }

/// <summary>
/// Interface for signaling that the configuration is tied to a release info
/// provider.
/// </summary>
public interface IReleaseInfoProviderConfiguration : IHiddenConfiguration { }
