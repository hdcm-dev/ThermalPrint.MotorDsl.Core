namespace MotorDsl.Pax;

/// <summary>
/// Impresora PAX simulada: recibe el mismo PNG que iria a <c>print(Bitmap)</c>, lo recorta al ancho
/// del perfil (Inferido: el recorte a la derecha esta documentado para printColorBitmap*, no para
/// print(Bitmap, IPinterListener); se confirma en el equipo, D1) y lo vuelca a
/// <c>&lt;OutputDirectory&gt;/pax-&lt;timestamp&gt;.png</c>. Responde segun
/// <see cref="PaxPrinterOptions.SimulatedStatus"/> con los mismos codigos crudos que el SDK.
/// </summary>
public sealed class SimulatedPaxPrinterDevice : IPaxPrinterDevice
{
    private readonly PaxPrinterOptions _options;
    private readonly Func<DateTime> _clock;
    private bool _open;

    public SimulatedPaxPrinterDevice(PaxPrinterOptions options, Func<DateTime>? clock = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? (() => DateTime.Now);
    }

    /// <summary>Ruta del ultimo PNG volcado; null si todavia no se imprimio.</summary>
    public string? LastOutputPath { get; private set; }

    public Task OpenAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        switch (_options.SimulatedStatus)
        {
            case PaxSimulatedStatus.ServiceMissing:
                throw new PaxSdkException(PaxErrorSource.LoadDal, 0, "LOAD DAL ERR");
            case PaxSimulatedStatus.Connect:
                throw new PaxSdkException(PaxErrorSource.PrintDevException, PaxErrorMapping.PrintDevConnect, "EPrintDevException 99 (simulada)");
        }
        _open = true;
        return Task.CompletedTask;
    }

    public Task<int> GetStatusAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        return Task.FromResult(_options.SimulatedStatus switch
        {
            PaxSimulatedStatus.Busy => PaxErrorMapping.StatusBusy,
            PaxSimulatedStatus.PaperOut => PaxErrorMapping.StatusPaperOut,
            PaxSimulatedStatus.Overheat => PaxErrorMapping.StatusOverheat,
            PaxSimulatedStatus.LowVoltage => PaxErrorMapping.StatusLowVoltage,
            _ => PaxErrorMapping.StatusOk
        });
    }

    public async Task<int> PrintAsync(byte[] png, CancellationToken ct = default)
    {
        EnsureOpen();
        if (_options.SimulatedStatus == PaxSimulatedStatus.TooLong) return PaxErrorMapping.StatusTooLong;

        var cropped = PaxPng.CropToWidth(png, _options.WidthPx);
        Directory.CreateDirectory(_options.OutputDirectory);
        var baseName = $"pax-{_clock():yyyyMMdd-HHmmss-fff}";
        var path = Path.Combine(_options.OutputDirectory, baseName + ".png");
        for (int i = 1; File.Exists(path); i++)
            path = Path.Combine(_options.OutputDirectory, $"{baseName}-{i}.png");
        await File.WriteAllBytesAsync(path, cropped, ct);
        LastOutputPath = path;
        return PaxErrorMapping.StatusOk;
    }

    public Task CloseAsync()
    {
        _open = false;
        return Task.CompletedTask;
    }

    private void EnsureOpen()
    {
        if (!_open) throw new PaxSdkException(PaxErrorSource.PrintDevException, PaxErrorMapping.PrintDevConnect, "Impresora simulada no abierta");
    }
}
