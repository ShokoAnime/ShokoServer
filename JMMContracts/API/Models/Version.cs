using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMContracts.API.Models
{
    /// <summary>
    /// Model that represents Version of JMM
    /// </summary>
    public class Version
    {
        /// <summary>
        /// version
        /// </summary>
        public string version { get; set; }

        /// <summary>
        /// Parametraless constructor for XML Serialization and populating version number
        /// </summary>
        public Version()
        {
            version = System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString();
        }
    }
}
