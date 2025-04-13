using System;

namespace HandballManager.Simulation.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when an error occurs during simulation execution.
    /// </summary>
    public class RuntimeException : SimulationException
    {
        /// <summary>
        /// Initializes a new instance of the RuntimeException class with a specified error message.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public RuntimeException(string message)
            : base(message, SimulationErrorType.RuntimeError)
        {
        }

        /// <summary>
        /// Initializes a new instance of the RuntimeException class with a specified error message and inner exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public RuntimeException(string message, Exception innerException)
            : base(message, innerException, SimulationErrorType.RuntimeError)
        {
        }
    }
}