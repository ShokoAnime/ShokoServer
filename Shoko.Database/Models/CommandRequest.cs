using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models
{
    public class CommandRequest
    {
        [Key, Column("CommandRequestID")] public int Id { get; set; }
        public int Priority { get; set; }
        public int CommandType { get; set; }
        public string CommandID { get; set; }
        public string CommandDetails { get; set; }
        public DateTime DateTimeUpdated { get; set; }

    }
}
