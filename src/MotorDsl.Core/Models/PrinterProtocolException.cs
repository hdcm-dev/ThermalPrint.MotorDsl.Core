namespace MotorDsl.Core.Models;

/// <summary>
/// El transport recibio datos con un formato que no puede enviar (por ejemplo, el transport PAX
/// espera un PNG y recibio ESC/POS). Se clasifica como <see cref="PrintErrorType.Protocol"/>,
/// que el handler por defecto no reintenta: reenviar los mismos bytes daria el mismo resultado.
/// </summary>
public class PrinterProtocolException : Exception
{
    public PrinterProtocolException(string message) : base(message) { }
    public PrinterProtocolException(string message, Exception inner) : base(message, inner) { }
}
