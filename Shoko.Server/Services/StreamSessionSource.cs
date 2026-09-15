using Microsoft.AspNetCore.Http;

namespace Shoko.Server.Services;

/// <summary>
///   What a stream session was built from, so it can be built again under the same id after eviction.
/// </summary>
/// <param name="VideoID">The ID of the video.</param>
/// <param name="TransformID">The ID of the transform that produced the rendition.</param>
/// <param name="QueryParameters">The query string the session was started with.</param>
public record StreamSessionSource(int VideoID, string TransformID, IQueryCollection QueryParameters);
