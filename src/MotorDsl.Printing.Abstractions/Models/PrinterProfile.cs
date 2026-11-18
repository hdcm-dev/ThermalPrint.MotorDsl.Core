namespace MotorDsl.Printing;

public class PrinterProfile
{
    public string Name { get; set; } = "default";
    public int LineDelayMs { get; set; } = 150;
    public int ByteDelayMs { get; set; } = 5;
    public int InitDelayMs { get; set; } = 100;
    public int FinalDelayMs { get; set; } = 500;
    public int QrDelayMs { get; set; } = 300;
    public int ImageDelayMs { get; set; } = 500;
    public int CutDelayMs { get; set; } = 500;
    public int InitCommandDelayMs { get; set; } = 300;

    /// <summary>
    /// Tope para la escritura de UN bloque. El control de flujo real lo aportan los creditos
    /// RFCOMM: cuando la impresora deja de otorgarlos, el write se bloquea. Este timeout es como
    /// se OBSERVA ese bloqueo — sin el, una impresora que deja de drenar cuelga el envio hasta que
    /// el enlace muere solo (medido: 106 s de bloqueo y ~4 min hasta la caida del ACL).
    /// Al vencer, el transport invalida la conexion y el reintento reconecta.
    /// </summary>
    public int WriteTimeoutMs { get; set; } = 10_000;

    /// <summary>
    /// Tope de espera de la <b>confirmacion de impresion</b>: tras enviar el documento se emite un
    /// comando encolado y se espera su respuesta, que llega recien cuando la impresora termino de
    /// imprimir. Distingue «se envio» de «se imprimio» — sin esto, una impresora que se queda sin
    /// energia con el documento ya en su buffer produce un exito falso.
    /// <para>
    /// No es una espera fija: normalmente la respuesta llega apenas termina de imprimir (medido:
    /// ~4 s para 62 lineas). Este valor solo acota el peor caso, por eso es holgado. En <b>0</b> se
    /// desactiva la confirmacion.
    /// </para>
    /// <para>
    /// Solo se aplica si la impresora respondio al <c>GS I</c> de la deteccion de capacidades; si
    /// no contesta ese comando, no se le exige confirmacion y el envio se comporta como antes.
    /// </para>
    /// </summary>
    public int ConfirmPrintTimeoutMs { get; set; } = 120_000;

    public static PrinterProfile Thermal58mm => new()
    {
        Name = "thermal_58mm",
        LineDelayMs = 150,
        ByteDelayMs = 5,
        InitDelayMs = 100,
        FinalDelayMs = 500
    };

    // Perfil real: 58HB6-6101 (basado en self-test)
    public static PrinterProfile Real58HB6 => new()
    {
        Name = "58HB6-6101",
        LineDelayMs = 150,
        ByteDelayMs = 5,
        InitDelayMs = 100,
        FinalDelayMs = 500,
        QrDelayMs = 300,
        ImageDelayMs = 500,
        CutDelayMs = 500,
        InitCommandDelayMs = 300
    };

    /// <summary>
    /// Impresora interna del PAX A910S (transport "pax", SDK NeptuneLite). Las demoras de pacing
    /// son de Bluetooth y no aplican: el documento viaja como un unico bitmap. La confirmacion por
    /// GS I tampoco aplica (la da el listener del SDK), por eso <see cref="ConfirmPrintTimeoutMs"/>
    /// es 0; <see cref="WriteTimeoutMs"/> acota la espera de ese listener. Valores Inferidos,
    /// pendientes de medir en el equipo (feature 04, D2).
    /// </summary>
    public static PrinterProfile PaxA910S => new()
    {
        Name = "pax_a910s",
        LineDelayMs = 0,
        ByteDelayMs = 0,
        InitDelayMs = 0,
        FinalDelayMs = 0,
        QrDelayMs = 0,
        ImageDelayMs = 0,
        CutDelayMs = 0,
        InitCommandDelayMs = 0,
        WriteTimeoutMs = 60_000,
        ConfirmPrintTimeoutMs = 0
    };
}
