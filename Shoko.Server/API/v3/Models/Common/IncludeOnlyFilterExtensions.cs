using Shoko.Abstractions.Filtering;

namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// Helpers for <see cref="IncludeOnlyFilter"/>.
/// </summary>
public static class IncludeOnlyFilterExtensions
{
    extension(IncludeOnlyFilter filter)
    {
        /// <summary>
        /// The service's counterpart of an APIv3 include filter.
        /// </summary>
        /// <returns>The same filter, for the services.</returns>
        public InclusionFilter InclusionFilter => filter switch
        {
            IncludeOnlyFilter.True => InclusionFilter.True,
            IncludeOnlyFilter.Only => InclusionFilter.Only,
            _ => InclusionFilter.False,
        };
    }
}
