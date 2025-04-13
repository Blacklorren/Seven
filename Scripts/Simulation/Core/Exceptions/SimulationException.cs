using System;

namespace HandballManager.Simulation.Core.Exceptions
{
    /// <summary>
    /// Base exception class for all simulation-related errors.
    /// Provides a consistent error handling mechanism for the simulation system.
    /// </summary>
    public class SimulationException : Exception
    {
        /// <summary>Gets the type of simulation error that occurred.</summary>
        public SimulationErrorType ErrorType { get; }

        /// <summary>Gets a value indicating whether the simulation was aborted.</summary>
        public bool IsAborted { get; }

        /// <summary>
        /// Initializes a new instance of the SimulationException class with a specified error message and error type.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="errorType">The type of simulation error that occurred.</param>
        /// <param name="isAborted">Indicates whether the simulation was aborted.</param>
        public SimulationException(string message, SimulationErrorType errorType, bool isAborted = true)
            : base(message)
        {
            ErrorType = errorType;
            IsAborted = isAborted;
        }

        /// <summary>
        /// Initializes a new instance of the SimulationException class with a specified error message, inner exception, and error type.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        /// <param name="errorType">The type of simulation error that occurred.</param>
        /// <param name="isAborted">Indicates whether the simulation was aborted.</param>
        public SimulationException(string message, Exception innerException, SimulationErrorType errorType, bool isAborted = true)
            : base(message, innerException)
        {
            ErrorType = errorType;
            IsAborted = isAborted;
        }
    }

    /// <summary>
    /// Defines the types of errors that can occur during simulation.
    /// </summary>
    public enum SimulationErrorType
    {
        /// <summary>An unknown error occurred.</summary>
        Unknown,

        /// <summary>Input validation failed (e.g., null teams, insufficient players).</summary>
        ValidationError,

        /// <summary>The simulation was cancelled by the user or system.</summary>
        Cancelled,

        /// <summary>An error occurred during simulation setup.</summary>
        SetupError,

        /// <summary>An error occurred during the simulation execution.</summary>
        RuntimeError
    }
}