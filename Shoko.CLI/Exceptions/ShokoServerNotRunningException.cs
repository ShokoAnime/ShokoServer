namespace Shoko.Cli.Exceptions
{
    using System;
    using System.Runtime.Serialization;
    using Properties;

    /// <summary>
    /// Represents an error in the shoko cli if the shoko server is not running after startup.
    /// </summary>
    [Serializable]
    public class ShokoServerNotRunningException : Exception
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ShokoServerNotRunningException"/> class.
        /// </summary>
        public ShokoServerNotRunningException() : base(Resources.Exceptions_ShokoServerNotRunning_Message)
        { }

        /// <summary>
        /// Initializes a new instance of <see cref="ShokoServerNotRunningException"/> class.
        /// </summary>
        /// <param name="message">
        /// The message that describes the error.
        /// </param>
        public ShokoServerNotRunningException(string? message) : base(message)
        { }

        /// <summary>
        /// Initializes a new instance of <see cref="ShokoServerNotRunningException"/> class.
        /// </summary>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception, or <see langword="null"/> if no inner exception is specified.
        /// </param>
        public ShokoServerNotRunningException(Exception? innerException) : base(Resources.Exceptions_ShokoServerNotRunning_Message, innerException) { }

        /// <summary>
        /// Initializes a new instance of <see cref="ShokoServerNotRunningException"/> class.
        /// </summary>
        /// <param name="message">
        /// The message that describes the error.
        /// </param>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception, or <see langword="null"/> if no inner exception is specified.
        /// </param>
        public ShokoServerNotRunningException(string? message, Exception? innerException) : base(message, innerException)
        { }

        /// <inheritdoc />
        protected ShokoServerNotRunningException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
