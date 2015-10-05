using System;
using System.Collections.Generic;
using JMMModels.Childs;

namespace JMMServerModels.DB
{
    public class DuplicateFile
    {
        public string Id { get; set; } //Hash
        public List<FileInfo> Duplicates { get; set; }
        public DateTime DateTimeUpdated { get; set; }
    }
}
