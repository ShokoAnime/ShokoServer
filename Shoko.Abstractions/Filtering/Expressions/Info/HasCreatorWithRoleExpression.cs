using System;
using System.Linq;
using Newtonsoft.Json;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Filtering.Expressions.Converters;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if the filterable has a creator with the specified role.
/// </summary>
public class HasCreatorWithRoleExpression : FilterExpression<bool>, IWithStringParameter, IWithSecondStringParameter
{
    /// <inheritdoc/>
    public HasCreatorWithRoleExpression(string creatorID, CrewRoleType role)
        => (CreatorID, Role) = (creatorID, role);

    /// <inheritdoc/>
    public HasCreatorWithRoleExpression() { }

    /// <summary>
    /// The AniDB ID of the creator to look for, kept as the parameter.
    /// </summary>
    public string? CreatorID { get; set; }

    /// <summary>
    /// The role the creator has to have, kept as the second parameter.
    /// </summary>
    [JsonConverter(typeof(LenientEnumNewtonsoftJsonConverter))]
    public CrewRoleType Role { get; set; }

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the filterable has a creator with the specified role.";

    /// <inheritdoc/>
    public override string HelpParameterName => "AniDB Creator ID";

    /// <inheritdoc/>
    public override string HelpSecondParameterName => "Role";

    /// <summary>
    /// The roles to pick from, in the order the enum declares them. <see cref="CrewRoleType.None"/>
    /// is left out, as it only gathers the credits no named role covers.
    /// </summary>
    public override string[] HelpPossibleSecondParameters
        => Enum.GetValues<CrewRoleType>().Where(x => x is not CrewRoleType.None).Select(x => x.ToString()).ToArray();

    string? IWithStringParameter.Parameter
    {
        get => CreatorID;
        set => CreatorID = value;
    }

    string IWithSecondStringParameter.SecondParameter
    {
        get => Role.ToString();
        set => Role = Enum.Parse<CrewRoleType>(value, ignoreCase: true);
    }

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return CreatorID is not null && filterable.CreatorRoles.TryGetValue(Role, out var roles) && roles.Contains(CreatorID);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasCreatorWithRoleExpression other)
    {
        return base.Equals(other) && CreatorID == other.CreatorID && Role == other.Role;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;

        if (ReferenceEquals(this, obj))
            return true;

        if (obj.GetType() != GetType())
            return false;

        return Equals((HasCreatorWithRoleExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), CreatorID, (int)Role);
    }
}
