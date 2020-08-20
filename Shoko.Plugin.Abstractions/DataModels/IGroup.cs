using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels
{
    public interface IGroup
    {
        /// <summary>
        /// Group Name
        /// </summary>
        string Name { get; }
        /// <summary>
        /// The series that is used for the name. May be null. Just use Series.FirstOrDefault() at that point.
        /// </summary>
        IAnime MainSeries { get; }
        /// <summary>
        /// The series in a group, ordered by AirDate
        /// </summary>
        IReadOnlyList<IAnime> Series { get; }
    }
}