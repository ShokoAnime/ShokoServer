
namespace Shoko.Plugin.Abstractions.Config;

/// <summary>
/// Base interface for all configurations served through the configuration service.
/// </summary>
public interface IConfiguration { }

/// <summary>
/// Interface for signaling that the configuration should use Newtonsoft.Json for serialization/deserialization.
/// </summary>
public interface INewtonsoftJsonConfiguration : IConfiguration { }

/// <summary>
/// Interface for signaling that the configuration should be hidden from the UI.
/// </summary>
public interface IHiddenConfiguration : IConfiguration { }
