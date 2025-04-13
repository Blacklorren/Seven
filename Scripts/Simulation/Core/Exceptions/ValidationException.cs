using System;

namespace HandballManager.Simulation.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when validation of simulation inputs fails.
    /// </summary>
    public class ValidationException : SimulationException
    {
        /// <summary>
        /// Initializes a new instance of the ValidationException class with a specified error message.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public ValidationException(string message)
            : base(message, SimulationErrorType.ValidationError)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ValidationException class with a specified error message and inner exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ValidationException(string message, Exception innerException)
            : base(message, innerException, SimulationErrorType.ValidationError)
        {
        }
    }
}