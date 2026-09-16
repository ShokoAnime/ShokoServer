using System;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Data transfer object (DTO) for updating an existing episode airing with
///   support for partial updates. A property left alone is not touched, and a
///   property where <c>null</c> is itself a value carries a <c>Has…Set</c>
///   flag, set by its setter.
/// </summary>
/// <remarks>
///   An airing's schedule, key and episode cannot be updated. Changing any of
///   them means a different airing.
/// </remarks>
public sealed class EpisodeAiringUpdateData
{
    /// <summary>
    ///   Used by the service to determine whether <see cref="AiredAt"/> should
    ///   be updated. Set to <c>true</c> when the property is set.
    /// </summary>
    public bool HasAiredAtSet { get; private set; }

    private DateTime? _airedAt;

    /// <summary>
    ///   When the episode airs, in UTC. Set it to <c>null</c> to leave the
    ///   airing without a slot.
    /// </summary>
    public DateTime? AiredAt
    {
        get => _airedAt;
        set
        {
            HasAiredAtSet = true;
            _airedAt = value;
        }
    }

    /// <summary>
    ///   Used by the service to determine whether
    ///   <see cref="OriginalAiredAt"/> should be updated. Set to <c>true</c>
    ///   when the property is set.
    /// </summary>
    public bool HasOriginalAiredAtSet { get; private set; }

    private DateTime? _originalAiredAt;

    /// <summary>
    ///   The slot the episode was first scheduled for, in UTC. Set it to
    ///   <c>null</c> to clear it.
    /// </summary>
    public DateTime? OriginalAiredAt
    {
        get => _originalAiredAt;
        set
        {
            HasOriginalAiredAtSet = true;
            _originalAiredAt = value;
        }
    }

    /// <summary>
    ///   Whether this airing's own slot was postponed. <c>null</c> leaves it
    ///   alone.
    /// </summary>
    public bool? IsDelayed { get; set; }

    /// <summary>
    ///   Used by the service to determine whether <see cref="Url"/> should be
    ///   updated. Set to <c>true</c> when the property is set.
    /// </summary>
    public bool HasUrlSet { get; private set; }

    private string? _url;

    /// <summary>
    ///   The episode's own page, overriding the schedule's URL. Set it to
    ///   <c>null</c> to clear it.
    /// </summary>
    public string? Url
    {
        get => _url;
        set
        {
            HasUrlSet = true;
            _url = value;
        }
    }
}
