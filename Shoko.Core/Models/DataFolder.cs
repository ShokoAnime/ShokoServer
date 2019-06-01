using System;

namespace Shoko.Core.Models
{
    public class DataFolder
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public Uri Path { get; set; } //Urikind.Absolute
        public bool WatchForFiles { get; set; }
        public DataFolderType Type { get; set; }
        public string ProviderType { get; set; }
    }

    public enum DataFolderType 
    {
        Normal = 0,
        Import = 1,
        Export = 2,
    }
}