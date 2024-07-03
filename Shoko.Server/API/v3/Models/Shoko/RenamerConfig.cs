using System.Collections.Generic;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

public class RenamerConfig
{
    /// <summary>
    /// The ID of the renamer
    /// </summary>
    public string RenamerID { get; set; }

    /// <summary>
    /// The name of the renamer. This is a unique ID!
    /// </summary>
    public string Name { get; set; }

    public List<RenamerSetting>? Settings { get; set; }

    public class RenamerSetting
    {
        /// <summary>
        /// Name of the setting
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The CLR type of the setting. Not necessary to provide it in mutations
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Value of the setting
        /// </summary>
        public object? Value { get; set; }
    }
}
