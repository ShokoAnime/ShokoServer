using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Info;

// TODO: REMOVE THIS FILTER EXPRESSION SOMETIME IN THE FUTURE AFTER THE LEGACY FILTERS ARE REMOVED!!1!
/// <summary>
///     Missing Links include logic for whether a link should exist
/// </summary>
public class MissingTvDBLinkExpression : FilterExpression<bool>
{
    public override string Name => "Missing TvDB Link";
    public override string HelpDescription => "This condition passes if any of the anime should have a TvDB link but does not have one";
    public override bool Deprecated => true;

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return false;
    }

    protected bool Equals(MissingTvDBLinkExpression other)
    {
        return base.Equals(other);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((MissingTvDBLinkExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(MissingTvDBLinkExpression left, MissingTvDBLinkExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(MissingTvDBLinkExpression left, MissingTvDBLinkExpression right)
    {
        return !Equals(left, right);
    }
}
