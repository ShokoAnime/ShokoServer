namespace Shoko.Server.API.v3.Models.Common
{
    public interface IFullModel<T>
    {
        T ToServerModel(T existingModel);
    }
}