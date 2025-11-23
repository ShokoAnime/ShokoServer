using Shoko.Plugin.Abstractions.Services;

namespace Shoko.Plugin.Abstractions.Config;

/// <summary>
/// Base interface for all configurations served through the
/// <see cref="IConfigurationService"/> or
///  <see cref="ConfigurationProvider{TConfig}"/>.
/// </summary>
public interface IConfiguration { }

/// <summary>
/// Interface for signaling that the the configuration is a base configuration
/// for other configurations, and should not be saved or loaded directly.
/// </summary>
/// <remarks>
/// Base configurations are used by other services and/or plugins to validate
/// and/or (de-)serialize their configuration instances using the configuration
/// service. They may also have custom actions to run on the configuration.
/// </remarks>
public interface IBaseConfiguration : IConfiguration { }

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

/// <summary>
/// Interface for signaling that the configuration is tied to a renamer provider.
/// </summary>
public interface IRelocationProviderConfiguration : IHiddenConfiguration, IBaseConfiguration { }
