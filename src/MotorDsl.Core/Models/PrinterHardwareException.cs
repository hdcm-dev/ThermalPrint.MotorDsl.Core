namespace MotorDsl.Core.Models;

/// <summary>
/// Causa de un fallo de hardware de la impresora, para que el consumidor decida el mensaje sin
/// comparar el texto de la excepcion. Aditivo: las excepciones creadas con los constructores
/// previos quedan en <see cref="Other"/>.
/// </summary>
public enum PrinterHardwareCause
{
    Other = 0,
    PaperOut,
    Overheat,
    Busy,
    LowVoltage,
    TooLong
}

/// <summary>
/// Fallo de hardware inequivoco de la impresora detectado por status (fin de papel, tapa
/// abierta). Se clasifica como <see cref="PrintErrorType.Hardware"/>, que el handler por
/// defecto NO reintenta: no tiene sentido reintentar sin papel o con la tapa abierta.
/// </summary>
public class PrinterHardwareException : Exception
{
    public PrinterHardwareException(string message) : base(message) { }
    public PrinterHardwareException(string message, Exception inner) : base(message, inner) { }

    public PrinterHardwareException(string message, PrinterHardwareCause cause) : base(message)
        => Cause = cause;

    public PrinterHardwareException(string message, PrinterHardwareCause cause, Exception inner) : base(message, inner)
        => Cause = cause;

    /// <summary>Causa clasificada; <see cref="PrinterHardwareCause.Other"/> si no se informo.</summary>
    public PrinterHardwareCause Cause { get; } = PrinterHardwareCause.Other;
}
