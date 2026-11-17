namespace MotorDsl.Core.Models;

/// <summary>
/// Classification of print errors for retry/abort decisions.
/// Sprint 06 | TK-40
/// </summary>
public enum PrintErrorType
{
    Connection,
    Timeout,
    Hardware,
    Protocol,
    Unknown
}

/// <summary>
/// Immutable representation of a print error with classification.
/// Sprint 06 | TK-40
/// </summary>
public class PrintError
{
    public PrintErrorType Type { get; }
    public string Message { get; }
    public Exception? InnerException { get; }
    public int Attempt { get; }
    public int MaxAttempts { get; }

    public PrintError(PrintErrorType type, string message, Exception? innerException, int attempt, int maxAttempts)
    {
        Type = type;
        Message = message;
        InnerException = innerException;
        Attempt = attempt;
        MaxAttempts = maxAttempts;
    }

    /// <summary>
    /// Factory that classifies an exception into the appropriate PrintErrorType.
    /// </summary>
    public static PrintError FromException(Exception ex, int attempt, int maxAttempts)
    {
        var type = ex switch
        {
            PrinterHardwareException => PrintErrorType.Hardware,
            PrintNotConfirmedException => PrintErrorType.Protocol,
            PrinterProtocolException => PrintErrorType.Protocol,
            System.IO.IOException => PrintErrorType.Connection,
            System.Net.Sockets.SocketException => PrintErrorType.Connection,
            TimeoutException => PrintErrorType.Timeout,
            TaskCanceledException => PrintErrorType.Timeout,
            _ => PrintErrorType.Unknown
        };

        return new PrintError(type, ex.Message, ex, attempt, maxAttempts);
    }
}

/// <summary>
/// Configuration for retry policy on print operations.
/// Sprint 06 | TK-41
/// </summary>
public class PrintRetryOptions
{
    public int MaxRetries { get; set; } = 3;
    public int InitialDelayMs { get; set; } = 500;
}
