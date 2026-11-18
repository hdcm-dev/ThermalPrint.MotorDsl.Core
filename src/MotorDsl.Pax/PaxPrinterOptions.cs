namespace MotorDsl.Pax;

public enum PaxPrinterMode
{
    /// <summary>
    /// Impresora interna real: la implementacion de <see cref="IPaxPrinterDevice"/> que entrega
    /// <see cref="PaxPrinterOptions.RealDeviceFactory"/>.
    /// </summary>
    Real,
    /// <summary><see cref="SimulatedPaxPrinterDevice"/>: vuelca el PNG a disco. Solo builds de desarrollo.</summary>
    Simulated
}

/// <summary>Estado que responde la impresora simulada.</summary>
public enum PaxSimulatedStatus
{
    Ok,
    /// <summary>getStatus = 1.</summary>
    Busy,
    /// <summary>getStatus = 2.</summary>
    PaperOut,
    /// <summary>getStatus = 8.</summary>
    Overheat,
    /// <summary>getStatus = 9.</summary>
    LowVoltage,
    /// <summary>onError = -2 al imprimir.</summary>
    TooLong,
    /// <summary>Exception("LOAD DAL ERR") al abrir.</summary>
    ServiceMissing,
    /// <summary>EPrintDevException 99 al abrir.</summary>
    Connect
}

public sealed class PaxPrinterOptions
{
    /// <summary>Id de la unica impresora que devuelve DiscoverAsync.</summary>
    public const string InternalDeviceId = "pax-internal";

    public PaxPrinterMode Mode { get; set; } = PaxPrinterMode.Real;

    /// <summary>
    /// Ancho imprimible en puntos. 384 es PROVISIONAL (Inferido): el material del SDK no lo
    /// documenta; se mide con el ticket de calibracion en el equipo (D1).
    /// </summary>
    public int WidthPx { get; set; } = PaxProfiles.DefaultWidthPx;

    /// <summary>Carpeta donde el simulador vuelca los PNG (pax-&lt;timestamp&gt;.png).</summary>
    public string OutputDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "motordsl-pax");

    public PaxSimulatedStatus SimulatedStatus { get; set; } = PaxSimulatedStatus.Ok;

    /// <summary>
    /// Fabrica del dispositivo real: el punto de extension por el que un paquete externo registra su
    /// implementacion de <see cref="IPaxPrinterDevice"/>. Se invoca una vez, solo en modo Real, al
    /// resolver el dispositivo; recibe estas mismas opciones. Si es null o devuelve null, el modo
    /// Real falla al conectar con Connection (nunca tira la app).
    /// </summary>
    public Func<PaxPrinterOptions, IPaxPrinterDevice>? RealDeviceFactory { get; set; }
}
