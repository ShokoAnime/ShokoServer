using System;

namespace Shoko.Plugin.Abstractions.Attributes;

/// <summary>
/// This attribute is used to identify a renamer.
/// It is an attribute to allow getting the ID of the renamer without instantiating it.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class RenamerIDAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RenamerIDAttribute"/> class.
    /// </summary>
    /// <param name="renamerId">The ID of the renamer.</param>
    public RenamerIDAttribute(string renamerId)
    {
        RenamerId = renamerId;
    }

    /// <summary>
    /// The ID of the renamer.
    /// </summary>
    public string RenamerId { get; }
}
