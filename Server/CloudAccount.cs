
namespace Shoko.Models.Server
{
    public class CloudAccount
    {
        #region DB Columns

        public int CloudID { get; set; }
        public string ConnectionString { get; set; }
        public string Name { get; set; }
        public string Provider { get; set; }

        #endregion

    }
}
