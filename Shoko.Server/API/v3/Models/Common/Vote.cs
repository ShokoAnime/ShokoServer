using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// Vote object. Shared between sources, episodes vs series, etc.
/// Normalises the value
/// </summary>
public class Vote
{
    public Vote(int value, int maxValue = 10) : this((decimal)value, maxValue) { }

    public Vote(decimal value, int maxValue = 10)
    {
        Value = value;
        MaxValue = maxValue;
    }

    public Vote() { }

    /// <summary>
    /// The normalised user-submitted rating in the range [0, <paramref name="maxValue" />].
    /// </summary>
    /// <param name="maxValue">The max value to use.</param>
    /// <returns></returns>
    public decimal GetRating(int maxValue = 10)
    {
        return Math.Clamp(Math.Clamp(Value, 0, MaxValue) / MaxValue * maxValue, 0, maxValue);
    }

    /// <summary>
    /// The user-submitted rating relative to <see cref="Vote.MaxValue" />.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Value must be greater than or equal to 0.")]
    [Required]
    public decimal Value { get; set; }

    /// <summary>
    /// Max allowed value for the user-submitted rating. Assumes 10 if not set.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Max value must be an integer above 0.")]
    [DefaultValue(10)]
    public int MaxValue { get; set; }

    /// <summary>
    /// for temporary vs permanent, or any other situations that may arise later.
    /// </summary>
    public string? Type { get; set; }
}
