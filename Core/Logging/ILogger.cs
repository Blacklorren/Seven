// --- START OF FILE HandballManager/Core/Logging/ILogger.cs ---
using System; // For Exception

namespace HandballManager.Core.Logging
{
    /// <summary>
    /// Defines a contract for logging messages at various levels.
    /// Allows decoupling application components from specific logging implementations (e.g., Unity Console, file logger).
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs an informational message. Generally used for tracing application flow or standard events.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void LogInformation(string message);

        /// <summary>
        /// Logs a warning message. Indicates a potential issue or unexpected situation that doesn't halt execution.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        void LogWarning(string message);

        /// <summary>
        /// Logs an error message. Indicates a failure or problem that occurred but might be recoverable.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        void LogError(string message);

        /// <summary>
        /// Logs an error message along with details of an exception.
        /// Indicates a significant failure, often used in catch blocks.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        /// <param name="ex">The exception associated with the error.</param>
        void LogError(string message, Exception ex);

        // Optional: Add other levels like Debug, Trace if needed
        // void LogDebug(string message);
        // void LogTrace(string message);
    }
}
// --- END OF FILE HandballManager/Core/Logging/ILogger.cs ---