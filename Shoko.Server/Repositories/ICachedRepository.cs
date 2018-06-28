
namespace Shoko.Server.Repositories
{
    public interface ICachedRepository
    {
        // Listed in order of call
        void Populate(bool displayname=true);
        //no longer uses ISessionWrapper
        //void Populate(ISessionWrapper session, bool displayname=true);
        void PopulateIndexes();
        void RegenerateDb();
        void PostProcess();
    }
}