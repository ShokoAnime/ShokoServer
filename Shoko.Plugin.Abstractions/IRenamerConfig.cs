using System;

namespace Shoko.Plugin.Abstractions
{
    public interface IRenamerConfig
    {
        /// <summary>
        /// The ID of the renamer instance
        /// </summary>
        public int ID { get; }

        /// <summary>
        /// The name of the renamer instance, mostly for user distinction
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The type of the renamer, always should be checked against the Renamer ID to ensure that the script should be executable against your renamer.
        /// </summary>
        public Type Type { get; }
    }

    public interface IRenamerConfig<T> : IRenamerConfig where T : class
    {
        /// <summary>
        /// The settings for the renamer
        /// </summary>
        T Settings { get; set; }
    }
}
