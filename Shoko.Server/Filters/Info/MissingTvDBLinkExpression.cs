namespace Shoko.Server.Filters.Info;

/// <summary>
///     Missing Links include logic for whether a link should exist
/// </summary>
public class MissingTvDBLinkExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override bool Evaluate(Filterable filterable)
    {
        return filterable.HasMissingTvDbLink;
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
