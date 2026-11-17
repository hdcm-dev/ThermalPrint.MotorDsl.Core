using MotorDsl.Core.Models;

namespace MotorDsl.Pax;

/// <summary>
/// Traduccion de los codigos del SDK NeptuneLite a las excepciones de MotorDsl. Dos tablas
/// separadas porque las numeraciones no coinciden (R3 §6):
/// <list type="number">
/// <item><c>getStatus/start/onError</c>: 0 ok; 1 ocupada; 2 sin papel; 8 sobrecalentamiento;
/// 9 voltaje bajo; -2 demasiado largo; resto → Hardware (Other).</item>
/// <item><c>EPrintDevException</c>: 5 OVER_HEATING y 8 TOO_LONG → Hardware; 99 CONNECT,
/// 101 NO_PERMISSION y 103 NOT_IN_WHITELIST → Connection; resto → Hardware (Other).</item>
/// </list>
/// LOAD DAL ERR y fallas nativas → Connection.
/// </summary>
public static class PaxErrorMapping
{
    public const int StatusOk = 0;
    public const int StatusBusy = 1;
    public const int StatusPaperOut = 2;
    public const int StatusOverheat = 8;
    public const int StatusLowVoltage = 9;
    public const int StatusTooLong = -2;

    public const int PrintDevOverHeating = 5;
    public const int PrintDevTooLong = 8;
    public const int PrintDevConnect = 99;
    public const int PrintDevNoPermission = 101;
    public const int PrintDevNotInWhitelist = 103;

    /// <summary>Excepcion para un codigo de la tabla de status; null si es 0 (ok).</summary>
    public static Exception? FromStatus(int code) => code switch
    {
        StatusOk => null,
        StatusBusy => Hw("Impresora ocupada", PrinterHardwareCause.Busy, code),
        StatusPaperOut => Hw("Impresora sin papel", PrinterHardwareCause.PaperOut, code),
        StatusOverheat => Hw("Impresora sobrecalentada", PrinterHardwareCause.Overheat, code),
        StatusLowVoltage => Hw("Voltaje bajo: cargue la bateria", PrinterHardwareCause.LowVoltage, code),
        StatusTooLong => Hw("Documento demasiado largo para la impresora", PrinterHardwareCause.TooLong, code),
        _ => Hw("Falla de la impresora", PrinterHardwareCause.Other, code)
    };

    /// <summary>Excepcion para un codigo de <c>EPrintDevException</c>.</summary>
    public static Exception FromPrintDevException(int code, Exception? inner = null) => code switch
    {
        PrintDevOverHeating => Hw("Impresora sobrecalentada", PrinterHardwareCause.Overheat, code, inner),
        PrintDevTooLong => Hw("Documento demasiado largo para la impresora", PrinterHardwareCause.TooLong, code, inner),
        PrintDevConnect => new PaxConnectionException("No se pudo conectar con la impresora PAX (EPrintDev 99)", code, inner),
        PrintDevNoPermission => new PaxConnectionException("Sin permiso de impresora PAX (EPrintDev 101)", code, inner),
        PrintDevNotInWhitelist => new PaxConnectionException("La app no esta en la whitelist PAX (EPrintDev 103)", code, inner),
        _ => Hw("Falla de la impresora", PrinterHardwareCause.Other, code, inner, "EPrintDev")
    };

    /// <summary>Traduce cualquier <see cref="PaxSdkException"/>.</summary>
    public static Exception ToException(PaxSdkException ex) => ex.Source switch
    {
        PaxErrorSource.Status => FromStatus(ex.Code) ?? new InvalidOperationException("PaxSdkException con status 0"),
        PaxErrorSource.PrintDevException => FromPrintDevException(ex.Code, ex),
        PaxErrorSource.LoadDal => new PaxConnectionException("No se pudo cargar el servicio de impresion PAX (LOAD DAL ERR). Avisá a soporte.", null, ex),
        _ => new PaxConnectionException("No se pudo cargar el SDK de la impresora PAX en este equipo", null, ex)
    };

    private static PrinterHardwareException Hw(string msg, PrinterHardwareCause cause, int code, Exception? inner = null, string source = "status")
    {
        var text = $"{msg} (PAX {source} {code})";
        return inner is null ? new PrinterHardwareException(text, cause) : new PrinterHardwareException(text, cause, inner);
    }
}
