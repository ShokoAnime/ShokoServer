
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions;

namespace Shoko.Server.API.v3.Models.Shoko
{
    public class FileRelocationPipeline
    {
        public FileRelocationPipeline()
        {
        }

        /// <summary>
        /// Pipeline ID.
        /// </summary>
        /// <value></value>
        public int ID { get; set; }

        /// <summary>
        /// Pipeline name. Must be unique across all pipelines.
        /// </summary>
        /// <value></value>
        public string Name { get; set; }

        /// <summary>
        /// User spesified description for the pipeline.
        /// </summary>
        /// <value></value>
        public string Description { get; set; }

        /// <summary>
        /// True if this pipeline is configured to run on import.
        /// </summary>
        /// <value></value>
        public bool RunOnImport { get; set; }

        /// <summary>
        /// Ordered list of stages in the pipeline.
        /// </summary>
        /// <value></value>
        public List<Stage<dynamic>> Stages { get; set; }

        public class Stage<TSettings>
        {
            public Stage()
            {
            }

            /// <summary>
            /// Script ID.
            /// </summary>
            [Required]
            public int ID { get; set; }

            /// <summary>
            /// The last known name of the <see cref="Renamer"/> this script belongs to.
            /// </summary>
            [Required]
            public string RenamerID { get; set; }

            /// <summary>
            /// Disable if you want to temporarily omit the stage from the pipeline.
            /// </summary>
            [Required]
            public bool Disabled { get; set; }

            /// <summary>
            /// Stage name. Must be unique across all stages.
            /// </summary>
            [Required]
            public string Name { get; set; }

            /// <summary>
            /// User spesified description of the stage.
            /// </summary>
            public string Description { get; set; }

            /// <summary>
            /// Renamer spesific stage.
            /// </summary>
            /// <value></value>
            public TSettings Settings { get; set; }
        }
    }
}