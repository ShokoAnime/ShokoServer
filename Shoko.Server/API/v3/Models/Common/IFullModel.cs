namespace Shoko.Server.API.v3
{
    public interface IFullModel<T>
    {
        T ToServerModel(T existingModel);
    }
}