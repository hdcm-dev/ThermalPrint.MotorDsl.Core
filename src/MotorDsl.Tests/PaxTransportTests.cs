using System.Buffers.Binary;
using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;
using MotorDsl.Core.Models;
using MotorDsl.Pax;
using MotorDsl.Printing;

namespace MotorDsl.Tests;

/// <summary>Feature 04 (PAX A910S), unidad U1: transport "pax", mapeo de errores, simulador, perfil y DI.</summary>
public class PaxTransportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "motordsl-pax-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    // ── Fake ─────────────────────────────────────────────────────────
    private sealed class FakePaxDevice : IPaxPrinterDevice
    {
        public int Status;
        public int PrintResult;
        public PaxSdkException? OpenThrows;
        public PaxSdkException? PrintThrows;
        public TaskCompletionSource<int>? PrintGate;
        public byte[]? Printed;
        public int Opens, Closes, StatusCalls;

        public Task OpenAsync(CancellationToken ct = default) { Opens++; if (OpenThrows != null) throw OpenThrows; return Task.CompletedTask; }
        public Task<int> GetStatusAsync(CancellationToken ct = default) { StatusCalls++; return Task.FromResult(Status); }
        public Task<int> PrintAsync(byte[] png, CancellationToken ct = default)
        {
            if (PrintThrows != null) throw PrintThrows;
            Printed = png;
            return PrintGate?.Task ?? Task.FromResult(PrintResult);
        }
        public Task CloseAsync() { Closes++; return Task.CompletedTask; }
    }

    private static async Task<(PaxPrinterTransport t, FakePaxDevice d)> Connected()
    {
        var d = new FakePaxDevice();
        var t = new PaxPrinterTransport(d);
        await t.ConnectAsync(PaxPrinterOptions.InternalDeviceId);
        return (t, d);
    }

    private static PrintErrorType Classify(Exception ex) => PrintError.FromException(ex, 1, 1).Type;

    // ── Transport ────────────────────────────────────────────────────
    [Fact]
    public async Task Kind_es_pax_y_descubre_una_impresora_interna()
    {
        var t = new PaxPrinterTransport(new FakePaxDevice());
        Assert.Equal("pax", t.Kind);
        var list = await t.DiscoverAsync();
        var dev = Assert.Single(list);
        Assert.Equal("pax-internal", dev.Id);
        Assert.Equal("pax", dev.Kind);
    }

    [Fact]
    public async Task Connect_abre_el_dispositivo_y_queda_conectado()
    {
        var (t, d) = await Connected();
        Assert.True(t.IsConnected);
        Assert.Equal("pax-internal", t.CurrentDevice!.Id);
        Assert.Equal(1, d.Opens);
        await t.DisconnectAsync();
        Assert.False(t.IsConnected);
        Assert.Equal(1, d.Closes);
    }

    [Fact]
    public async Task Connect_con_id_desconocido_lanza_ArgumentException()
    {
        var t = new PaxPrinterTransport(new FakePaxDevice());
        await Assert.ThrowsAsync<ArgumentException>(() => t.ConnectAsync("AA:BB"));
    }

    [Fact]
    public async Task Png_valido_se_imprime_tal_cual_tras_getStatus()
    {
        var (t, d) = await Connected();
        var png = PngBuilder.Rgba(10, 3);
        await t.WriteBytesAsync(png, PrinterProfile.PaxA910S);
        Assert.Same(png, d.Printed);
        Assert.Equal(1, d.StatusCalls);
    }

    [Theory]
    [InlineData(new byte[] { 0x1B, 0x40, 0x41 })]          // ESC/POS
    [InlineData(new byte[] { 0x1D, 0x76, 0x30, 0x00 })]    // GS v 0
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47 })]    // firma truncada
    [InlineData(new byte[0])]
    public async Task Entrada_no_PNG_da_Protocol_sin_llamar_al_dispositivo(byte[] data)
    {
        var (t, d) = await Connected();
        var ex = await Assert.ThrowsAsync<PrinterProtocolException>(() => t.WriteBytesAsync(data, PrinterProfile.PaxA910S));
        Assert.Equal(PrintErrorType.Protocol, Classify(ex));
        Assert.Null(d.Printed);
        Assert.Equal(0, d.StatusCalls);
    }

    [Fact]
    public async Task Escribir_sin_conectar_da_Connection()
    {
        var t = new PaxPrinterTransport(new FakePaxDevice());
        var ex = await Assert.ThrowsAsync<PaxConnectionException>(() => t.WriteBytesAsync(PngBuilder.Rgba(2, 2), PrinterProfile.PaxA910S));
        Assert.Equal(PrintErrorType.Connection, Classify(ex));
    }

    [Theory]
    [InlineData(1, PrinterHardwareCause.Busy)]
    [InlineData(2, PrinterHardwareCause.PaperOut)]
    [InlineData(8, PrinterHardwareCause.Overheat)]
    [InlineData(9, PrinterHardwareCause.LowVoltage)]
    [InlineData(-2, PrinterHardwareCause.TooLong)]
    [InlineData(3, PrinterHardwareCause.Other)]
    [InlineData(-1, PrinterHardwareCause.Other)]
    public async Task GetStatus_no_cero_da_Hardware_con_causa_y_no_imprime(int code, PrinterHardwareCause cause)
    {
        var (t, d) = await Connected();
        d.Status = code;
        var ex = await Assert.ThrowsAsync<PrinterHardwareException>(() => t.WriteBytesAsync(PngBuilder.Rgba(2, 2), PrinterProfile.PaxA910S));
        Assert.Equal(cause, ex.Cause);
        Assert.Equal(PrintErrorType.Hardware, Classify(ex));
        Assert.Null(d.Printed);
    }

    [Theory]
    [InlineData(2, PrinterHardwareCause.PaperOut)]
    [InlineData(-2, PrinterHardwareCause.TooLong)]
    [InlineData(8, PrinterHardwareCause.Overheat)]
    public async Task OnError_del_listener_da_Hardware_con_causa(int code, PrinterHardwareCause cause)
    {
        var (t, d) = await Connected();
        d.PrintResult = code;
        var ex = await Assert.ThrowsAsync<PrinterHardwareException>(() => t.WriteBytesAsync(PngBuilder.Rgba(2, 2), PrinterProfile.PaxA910S));
        Assert.Equal(cause, ex.Cause);
    }

    [Theory]
    [InlineData(5, PrintErrorType.Hardware, PrinterHardwareCause.Overheat)]
    [InlineData(8, PrintErrorType.Hardware, PrinterHardwareCause.TooLong)]
    [InlineData(7, PrintErrorType.Hardware, PrinterHardwareCause.Other)]
    [InlineData(99, PrintErrorType.Connection, null)]
    [InlineData(101, PrintErrorType.Connection, null)]
    [InlineData(103, PrintErrorType.Connection, null)]
    public async Task EPrintDevException_se_mapea_por_su_propia_tabla(int code, PrintErrorType type, PrinterHardwareCause? cause)
    {
        var (t, d) = await Connected();
        d.PrintThrows = new PaxSdkException(PaxErrorSource.PrintDevException, code);
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => t.WriteBytesAsync(PngBuilder.Rgba(2, 2), PrinterProfile.PaxA910S));
        Assert.Equal(type, Classify(ex));
        if (cause is { } c) Assert.Equal(c, Assert.IsType<PrinterHardwareException>(ex).Cause);
        else Assert.Equal(code, Assert.IsType<PaxConnectionException>(ex).Code);
    }

    [Theory]
    [InlineData(PaxErrorSource.LoadDal, 0)]
    [InlineData(PaxErrorSource.NativeLoad, 0)]
    [InlineData(PaxErrorSource.PrintDevException, 99)]
    public async Task Fallas_al_conectar_dan_Connection_sin_quedar_conectado(PaxErrorSource source, int code)
    {
        var d = new FakePaxDevice { OpenThrows = new PaxSdkException(source, code, source == PaxErrorSource.LoadDal ? "LOAD DAL ERR" : null) };
        var t = new PaxPrinterTransport(d);
        var ex = await Assert.ThrowsAsync<PaxConnectionException>(() => t.ConnectAsync("pax-internal"));
        Assert.Equal(PrintErrorType.Connection, Classify(ex));
        Assert.False(t.IsConnected);
        Assert.Null(t.CurrentDevice);
    }

    [Fact]
    public void LoadDal_menciona_avisar_a_soporte()
    {
        var ex = PaxErrorMapping.ToException(new PaxSdkException(PaxErrorSource.LoadDal, 0, "LOAD DAL ERR"));
        Assert.Contains("Avisá a soporte", ex.Message);
    }

    [Fact]
    public async Task Listener_que_no_responde_da_Timeout()
    {
        var (t, d) = await Connected();
        d.PrintGate = new TaskCompletionSource<int>();
        var profile = PrinterProfile.PaxA910S;
        profile.WriteTimeoutMs = 50;
        var ex = await Assert.ThrowsAsync<TimeoutException>(() => t.WriteBytesAsync(PngBuilder.Rgba(2, 2), profile));
        Assert.Equal(PrintErrorType.Timeout, Classify(ex));
    }

    [Fact]
    public void PrinterHardwareException_previa_queda_en_Other()
    {
        Assert.Equal(PrinterHardwareCause.Other, new PrinterHardwareException("x").Cause);
        Assert.Equal(PrinterHardwareCause.Other, new PrinterHardwareException("x", new Exception()).Cause);
    }

    // ── Perfil ───────────────────────────────────────────────────────
    [Fact]
    public void Perfil_A910S_por_defecto_384_raster_preview()
    {
        var p = PaxProfiles.A910S();
        Assert.Equal("pax_a910s", p.Name);
        Assert.Equal(32, p.Width);
        Assert.Equal("raster-preview", p.RenderTarget);
        Assert.Equal(true, p.GetCapability("supports_bitmap"));
        Assert.Equal(384, p.GetCapability("bitmap_max_width_px"));
    }

    [Fact]
    public void Perfil_A910S_es_parametrizable()
    {
        Assert.Equal(576, PaxProfiles.A910S(576).GetCapability("bitmap_max_width_px"));
        Assert.Throws<ArgumentOutOfRangeException>(() => PaxProfiles.A910S(0));
    }

    [Fact]
    public void PrinterProfile_PaxA910S_sin_demoras_bluetooth_ni_confirmacion_GS_I()
    {
        var p = PrinterProfile.PaxA910S;
        Assert.Equal("pax_a910s", p.Name);
        Assert.Equal(0, p.LineDelayMs);
        Assert.Equal(0, p.ByteDelayMs);
        Assert.Equal(0, p.ConfirmPrintTimeoutMs);
        Assert.True(p.WriteTimeoutMs > 0);
    }

    // ── Simulador ────────────────────────────────────────────────────
    private PaxPrinterOptions SimOptions(PaxSimulatedStatus status = PaxSimulatedStatus.Ok, int width = 384)
        => new() { Mode = PaxPrinterMode.Simulated, OutputDirectory = _dir, SimulatedStatus = status, WidthPx = width };

    [Fact]
    public async Task Simulador_vuelca_el_PNG_exacto_si_cabe_en_el_ancho()
    {
        var sim = new SimulatedPaxPrinterDevice(SimOptions(), () => new DateTime(2026, 11, 17, 10, 20, 30, 456));
        var t = new PaxPrinterTransport(sim);
        await t.ConnectAsync("pax-internal");
        var png = PngBuilder.Rgba(384, 20);
        await t.WriteBytesAsync(png, PrinterProfile.PaxA910S);

        Assert.Equal(Path.Combine(_dir, "pax-20261117-102030-456.png"), sim.LastOutputPath);
        Assert.Equal(png, File.ReadAllBytes(sim.LastOutputPath!));
    }

    [Fact]
    public async Task Simulador_no_pisa_volcados_con_el_mismo_timestamp()
    {
        var sim = new SimulatedPaxPrinterDevice(SimOptions(), () => new DateTime(2026, 1, 1));
        await sim.OpenAsync();
        await sim.PrintAsync(PngBuilder.Rgba(4, 4));
        var first = sim.LastOutputPath;
        await sim.PrintAsync(PngBuilder.Rgba(4, 4));
        Assert.NotEqual(first, sim.LastOutputPath);
        Assert.Equal(2, Directory.GetFiles(_dir, "pax-*.png").Length);
    }

    [Theory]
    [InlineData(6)] // RGBA
    [InlineData(2)] // RGB
    [InlineData(0)] // gris
    public async Task Simulador_recorta_al_ancho_del_perfil_preservando_pixeles(byte colorType)
    {
        const int w = 400, h = 7, target = 384;
        var (png, pixels, bpp) = PngBuilder.Build(w, h, colorType);
        var sim = new SimulatedPaxPrinterDevice(SimOptions(width: target));
        await sim.OpenAsync();
        Assert.Equal(0, await sim.PrintAsync(png));

        var outPng = File.ReadAllBytes(sim.LastOutputPath!);
        var hdr = PaxPng.ReadHeader(outPng);
        Assert.Equal(target, hdr.Width);
        Assert.Equal(h, hdr.Height);
        Assert.Equal(colorType, hdr.ColorType);

        var decoded = PngBuilder.DecodeUnfiltered(outPng);
        for (int y = 0; y < h; y++)
            Assert.Equal(
                pixels.AsSpan(y * w * bpp, target * bpp).ToArray(),
                decoded.AsSpan(y * target * bpp, target * bpp).ToArray());
    }

    [Theory]
    [InlineData(PaxSimulatedStatus.Busy, PrinterHardwareCause.Busy)]
    [InlineData(PaxSimulatedStatus.PaperOut, PrinterHardwareCause.PaperOut)]
    [InlineData(PaxSimulatedStatus.Overheat, PrinterHardwareCause.Overheat)]
    [InlineData(PaxSimulatedStatus.LowVoltage, PrinterHardwareCause.LowVoltage)]
    [InlineData(PaxSimulatedStatus.TooLong, PrinterHardwareCause.TooLong)]
    public async Task Simulador_estados_de_hardware(PaxSimulatedStatus status, PrinterHardwareCause cause)
    {
        var t = new PaxPrinterTransport(new SimulatedPaxPrinterDevice(SimOptions(status)));
        await t.ConnectAsync("pax-internal");
        var ex = await Assert.ThrowsAsync<PrinterHardwareException>(() => t.WriteBytesAsync(PngBuilder.Rgba(8, 8), PrinterProfile.PaxA910S));
        Assert.Equal(cause, ex.Cause);
        Assert.Equal(PrintErrorType.Hardware, Classify(ex));
        Assert.False(Directory.Exists(_dir) && Directory.GetFiles(_dir).Length > 0);
    }

    [Theory]
    [InlineData(PaxSimulatedStatus.ServiceMissing, null)]
    [InlineData(PaxSimulatedStatus.Connect, 99)]
    public async Task Simulador_estados_de_conexion(PaxSimulatedStatus status, int? code)
    {
        var t = new PaxPrinterTransport(new SimulatedPaxPrinterDevice(SimOptions(status)));
        var ex = await Assert.ThrowsAsync<PaxConnectionException>(() => t.ConnectAsync("pax-internal"));
        Assert.Equal(PrintErrorType.Connection, Classify(ex));
        Assert.Equal(code, ex.Code);
    }

    // ── DI ───────────────────────────────────────────────────────────
    [Fact]
    public void AddPaxPrinterTransport_registra_Kind_pax_y_convive_con_otros()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IThermalPrinterTransport, FakeTransport>();
        services.AddPaxPrinterTransport(o => { o.Mode = PaxPrinterMode.Simulated; o.OutputDirectory = _dir; });
        var sp = services.BuildServiceProvider();

        var transports = sp.GetRequiredService<IEnumerable<IThermalPrinterTransport>>().ToList();
        Assert.Equal(2, transports.Count);
        Assert.Contains(transports, x => x.Kind == "fake");
        Assert.Contains(transports, x => x.Kind == "pax");
        Assert.IsType<SimulatedPaxPrinterDevice>(sp.GetRequiredService<IPaxPrinterDevice>());
        Assert.Equal(_dir, sp.GetRequiredService<PaxPrinterOptions>().OutputDirectory);
    }

    [Fact]
    public async Task Modo_Real_sin_SDK_falla_con_Connection_sin_crash()
    {
        var sp = new ServiceCollection().AddPaxPrinterTransport().BuildServiceProvider();
        var t = sp.GetRequiredService<IThermalPrinterTransport>();
        var ex = await Assert.ThrowsAsync<PaxConnectionException>(() => t.ConnectAsync("pax-internal"));
        Assert.Equal(PrintErrorType.Connection, Classify(ex));
    }

    [Fact]
    public void Modo_Real_usa_la_fabrica_del_SDK()
    {
        var fake = new FakePaxDevice();
        var sp = new ServiceCollection().AddPaxPrinterTransport(o => o.RealDeviceFactory = _ => fake).BuildServiceProvider();
        Assert.Same(fake, sp.GetRequiredService<IPaxPrinterDevice>());
    }
}

/// <summary>Arma PNG de prueba con filtros variados por fila (0..4) para ejercitar el desfiltrado.</summary>
internal static class PngBuilder
{
    public static byte[] Rgba(int w, int h) => Build(w, h, 6).png;

    public static (byte[] png, byte[] pixels, int bpp) Build(int w, int h, byte colorType)
    {
        int bpp = colorType switch { 0 => 1, 2 => 3, 6 => 4, _ => throw new ArgumentException() };
        int stride = w * bpp;
        var pixels = new byte[stride * h];
        var rnd = new Random(1234 + colorType);
        rnd.NextBytes(pixels);

        var raw = new byte[(stride + 1) * h];
        for (int y = 0; y < h; y++)
        {
            byte f = (byte)(y % 5);
            raw[y * (stride + 1)] = f;
            for (int i = 0; i < stride; i++)
            {
                int x = pixels[y * stride + i];
                int a = i >= bpp ? pixels[y * stride + i - bpp] : 0;
                int b = y > 0 ? pixels[(y - 1) * stride + i] : 0;
                int c = y > 0 && i >= bpp ? pixels[(y - 1) * stride + i - bpp] : 0;
                int pred = f switch { 0 => 0, 1 => a, 2 => b, 3 => (a + b) >> 1, _ => PaxPng.Paeth(a, b, c) };
                raw[y * (stride + 1) + 1 + i] = (byte)(x - pred);
            }
        }

        using var comp = new MemoryStream();
        using (var z = new ZLibStream(comp, CompressionLevel.Fastest, leaveOpen: true)) z.Write(raw);

        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0), w);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), h);
        ihdr[8] = 8; ihdr[9] = colorType;

        using var ms = new MemoryStream();
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        PaxPng.WriteChunk(ms, "IHDR", ihdr);
        PaxPng.WriteChunk(ms, "IDAT", comp.ToArray());
        PaxPng.WriteChunk(ms, "IEND", Array.Empty<byte>());
        return (ms.ToArray(), pixels, bpp);
    }

    /// <summary>Decodifica un PNG cuyas filas usan filtro 0 (lo que emite el recorte).</summary>
    public static byte[] DecodeUnfiltered(byte[] png)
    {
        var hdr = PaxPng.ReadHeader(png);
        int bpp = hdr.ColorType switch { 0 => 1, 2 => 3, 6 => 4, _ => throw new ArgumentException() };
        using var idat = new MemoryStream();
        int pos = 8;
        while (pos < png.Length)
        {
            int len = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(pos));
            var type = System.Text.Encoding.ASCII.GetString(png, pos + 4, 4);
            if (type == "IDAT") idat.Write(png, pos + 8, len);
            pos += 12 + len;
        }
        idat.Position = 0;
        using var raw = new MemoryStream();
        using (var z = new ZLibStream(idat, CompressionMode.Decompress)) z.CopyTo(raw);
        var data = raw.ToArray();
        int stride = hdr.Width * bpp;
        var outp = new byte[stride * hdr.Height];
        for (int y = 0; y < hdr.Height; y++)
        {
            Assert.Equal(0, data[y * (stride + 1)]);
            Array.Copy(data, y * (stride + 1) + 1, outp, y * stride, stride);
        }
        return outp;
    }
}
