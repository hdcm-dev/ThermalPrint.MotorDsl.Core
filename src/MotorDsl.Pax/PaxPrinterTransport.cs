using MotorDsl.Core.Models;
using MotorDsl.Printing;

namespace MotorDsl.Pax;

/// <summary>
/// Transport de la impresora interna del PAX A910S (Kind "pax"). Vía bitmap (a): recibe por el
/// <see cref="WriteBytesAsync"/> existente el PNG que produce <c>RasterPreviewRenderer</c>
/// (target raster-preview), valida la firma PNG (otra cosa → <see cref="PrinterProtocolException"/>,
/// clasificada Protocol), consulta <c>getStatus()</c> y llama <c>print(Bitmap, IPinterListener)</c>
/// a traves de <see cref="IPaxPrinterDevice"/>. Sin tipos Android: MotorDsl.Tests lo enlaza por fuente.
/// <para>
/// Errores: <see cref="PrinterHardwareException"/> con <see cref="PrinterHardwareCause"/> (Hardware,
/// no reintenta), <see cref="PaxConnectionException"/> (IOException → Connection),
/// <see cref="TimeoutException"/> si el listener no responde en <see cref="PrinterProfile.WriteTimeoutMs"/>.
/// </para>
/// </summary>
public sealed class PaxPrinterTransport : IThermalPrinterTransport
{
    private readonly IPaxPrinterDevice _device;
    private PrinterDevice? _current;

    public PaxPrinterTransport(IPaxPrinterDevice device)
        => _device = device ?? throw new ArgumentNullException(nameof(device));

    public string Kind => "pax";

    public bool IsConnected { get; private set; }

    public PrinterDevice? CurrentDevice => _current;

    public static PrinterDevice InternalPrinter { get; } =
        new(PaxPrinterOptions.InternalDeviceId, "PAX A910S (impresora interna)", "pax", IsPaired: true);

    /// <summary>Una unica impresora integrada; no hay barrido.</summary>
    public Task<IReadOnlyList<PrinterDevice>> DiscoverAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<PrinterDevice>>(new[] { InternalPrinter });
    }

    public async Task<bool> ConnectAsync(string deviceId, CancellationToken ct = default)
    {
        if (!string.Equals(deviceId, PaxPrinterOptions.InternalDeviceId, StringComparison.Ordinal))
            throw new ArgumentException($"Impresora PAX desconocida: '{deviceId}'. Se espera '{PaxPrinterOptions.InternalDeviceId}'.", nameof(deviceId));

        IsConnected = false;
        _current = null;
        await Guard(() => _device.OpenAsync(ct));
        IsConnected = true;
        _current = InternalPrinter;
        return true;
    }

    public async Task DisconnectAsync()
    {
        IsConnected = false;
        _current = null;
        try { await _device.CloseAsync(); } catch { /* cerrar nunca debe fallar hacia arriba */ }
    }

    public async Task WriteBytesAsync(byte[] data, PrinterProfile profile, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!PaxPng.HasSignature(data))
            throw new PrinterProtocolException(
                "El transport PAX espera un PNG (target raster-preview); los datos recibidos no tienen firma PNG.");
        if (!IsConnected)
            throw new PaxConnectionException("Impresora PAX no conectada");

        var status = await Guard(() => _device.GetStatusAsync(ct));
        ThrowIfError(status);

        var printTask = Guard(() => _device.PrintAsync(data, ct));
        int timeoutMs = profile?.WriteTimeoutMs ?? 0;
        if (timeoutMs > 0)
        {
            var finished = await Task.WhenAny(printTask, Task.Delay(timeoutMs, ct));
            if (finished != printTask)
            {
                ct.ThrowIfCancellationRequested();
                _ = printTask.ContinueWith(t => _ = t.Exception, TaskScheduler.Default);
                throw new TimeoutException($"La impresora PAX no confirmo la impresion en {timeoutMs} ms");
            }
        }
        ThrowIfError(await printTask);
    }

    private static void ThrowIfError(int code)
    {
        var ex = PaxErrorMapping.FromStatus(code);
        if (ex is not null) throw ex;
    }

    private static async Task Guard(Func<Task> op)
    {
        try { await op(); }
        catch (PaxSdkException ex) { throw PaxErrorMapping.ToException(ex); }
    }

    private static async Task<T> Guard<T>(Func<Task<T>> op)
    {
        try { return await op(); }
        catch (PaxSdkException ex) { throw PaxErrorMapping.ToException(ex); }
    }
}
