using MotorDsl.Core.Contracts;
using MotorDsl.Core.Models;
using MotorDsl.MultaApp.Services;
using MotorDsl.MultaApp.Templates;

#if ANDROID
using Android;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
#endif

namespace MotorDsl.MultaApp.Pages;

public partial class MainPage : ContentPage
{
    private readonly IDocumentEngine _engine;
    private readonly IThermalPrinterService _printer;
    private List<BluetoothDevice> _devices = new();

    // Documentos disponibles: template DSL + datos + nombre
    private readonly (string Name, string Template, Func<Dictionary<string, object>> Data)[] _documents;

    public MainPage(IDocumentEngine engine, IThermalPrinterService printer)
    {
        InitializeComponent();
        _engine = engine;
        _printer = printer;

        _documents = new[]
        {
            ("Acta de Infracción", MultaDsl.Template, (Func<Dictionary<string, object>>)MultaDsl.GetSampleData),
            ("Ticket Simple de Multa", TicketSimpleDsl.Template, (Func<Dictionary<string, object>>)TicketSimpleDsl.GetSampleData),
            ("Comprobante de Pago", ComprobanteDsl.Template, (Func<Dictionary<string, object>>)ComprobanteDsl.GetSampleData),
            // Prueba de carga sobre hardware real: 200 infracciones en un acta. Ver
            // MultaDsl.GetCargaData para qué está medido fuera de la impresora y qué no.
            ("Acta de 200 infracciones (carga)", MultaDsl.Template, (Func<Dictionary<string, object>>)(() => MultaDsl.GetCargaData(200))),
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

#if ANDROID
        var granted = await RequestBluetoothPermissions();
        if (granted)
            await AutoConnectBluetoothAsync();
#elif IOS
        // iOS: Bluetooth clásico SPP no disponible; nada que inicializar.
        await Task.CompletedTask;
#else
        await AutoConnectBluetoothAsync();
#endif
    }

    // ─── Bluetooth Permissions (Android 12+) ───

#if ANDROID
    private async Task<bool> RequestBluetoothPermissions()
    {
        try
        {
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.S)
            {
                var activity = Platform.CurrentActivity!;
                string[] btPermissions = new[]
                {
                    Manifest.Permission.BluetoothScan,
                    Manifest.Permission.BluetoothConnect
                };

                bool allGranted = btPermissions.All(p =>
                    ContextCompat.CheckSelfPermission(activity, p) == (int)Android.Content.PM.Permission.Granted);

                if (!allGranted)
                {
                    ActivityCompat.RequestPermissions(activity, btPermissions, 1);
                    await Task.Delay(3000);

                    allGranted = btPermissions.All(p =>
                        ContextCompat.CheckSelfPermission(activity, p) == (int)Android.Content.PM.Permission.Granted);
                }

                if (!allGranted)
                {
                    ShowMessage("Permisos BT denegados. Aceptá los permisos y presioná Reconectar.");
                    BtStatusLabel.Text = "Bluetooth: permisos denegados";
                    BtnReconectar.IsVisible = true;
                    return false;
                }
            }
            else
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

                if (status != PermissionStatus.Granted)
                {
                    ShowMessage("Permisos de ubicación denegados (necesarios para BT en Android < 12).");
                    BtnReconectar.IsVisible = true;
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MULTA] BT Permissions error: {ex.Message}");
            ShowMessage($"Error permisos BT: {ex.Message}");
            BtnReconectar.IsVisible = true;
            return false;
        }
    }
#endif

    // ─── Bluetooth Auto-Connect ───

    private async Task AutoConnectBluetoothAsync()
    {
        try
        {
            ShowMessage("Buscando impresoras emparejadas...");
            _devices = await _printer.ScanDevicesAsync();

            if (_devices.Count == 0)
            {
                BtStatusLabel.Text = "Bluetooth: sin dispositivos emparejados";
                BtnReconectar.IsVisible = true;
                ShowMessage("No se encontraron impresoras.");
            }
            else if (_devices.Count == 1)
            {
                BtStatusLabel.Text = $"Conectando a {_devices[0].Name}...";
                var ok = await _printer.ConnectAsync(_devices[0].Address);
                BtStatusLabel.Text = ok
                    ? $"Conectado: {_devices[0].Name}"
                    : "Error al conectar";
                BtnReconectar.IsVisible = !ok;
                ShowMessage(ok ? "Conectado automáticamente." : "Falló la conexión.");
            }
            else
            {
                BtStatusLabel.Text = $"Bluetooth: {_devices.Count} dispositivos encontrados";
                DeviceList.ItemsSource = _devices.Select(d => d.ToString()).ToList();
                DeviceList.IsVisible = true;
                ShowMessage("Seleccioná una impresora.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MULTA] AutoConnect error: {ex.Message}");
            BtStatusLabel.Text = "Bluetooth: error";
            BtnReconectar.IsVisible = true;
            ShowMessage($"BT Error: {ex.Message}");
        }
    }

    private async void OnDeviceSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not string selected) return;

        var device = _devices.FirstOrDefault(d => d.ToString() == selected);
        if (device == null) return;

        try
        {
            BtStatusLabel.Text = $"Conectando a {device.Name}...";
            var ok = await _printer.ConnectAsync(device.Address);
            BtStatusLabel.Text = ok ? $"Conectado: {device.Name}" : "Error al conectar";
            DeviceList.IsVisible = false;
            BtnReconectar.IsVisible = !ok;
            ShowMessage(ok ? $"Conectado a {device.Name}." : "Falló la conexión.");
        }
        catch (Exception ex)
        {
            BtStatusLabel.Text = "Error al conectar";
            ShowMessage(ex.Message);
        }
    }

    private async void OnReconectarClicked(object? sender, EventArgs e)
    {
        BtnReconectar.IsVisible = false;
#if ANDROID
        var granted = await RequestBluetoothPermissions();
        if (granted)
            await AutoConnectBluetoothAsync();
#elif IOS
        // iOS: Bluetooth clásico SPP no disponible; nada que inicializar.
        await Task.CompletedTask;
#else
        await AutoConnectBluetoothAsync();
#endif
    }

    // ─── Selector de documento ───

    private void OnDocPickerChanged(object? sender, EventArgs e)
    {
        PreviewLabel.Text = "Presioná 'Vista Previa' para ver el documento.";
        // PdfWebView eliminado
    }

    private (string Template, Dictionary<string, object> Data)? GetSelectedDocument()
    {
        var idx = DocPicker.SelectedIndex;
        if (idx < 0 || idx >= _documents.Length)
        {
            ShowMessage("Seleccioná un documento primero.");
            return null;
        }
        return (_documents[idx].Template, _documents[idx].Data());
    }

    // ─── Vista Previa (texto) ───

    private void OnVistaPreviewClicked(object? sender, EventArgs e)
    {
        Console.WriteLine("[MULTA] OnVistaPrevia iniciado");
        var doc = GetSelectedDocument();
        if (doc == null) return;

        try
        {
            // PdfWebView eliminado
            var profile = new DeviceProfile("thermal_58mm", 32, "text");
            Console.WriteLine("[MULTA] Llamando engine.Render...");
            var result = _engine.Render(doc.Value.Template, doc.Value.Data, profile);
            Console.WriteLine($"[MULTA] Render OK. IsSuccessful={result.IsSuccessful}");

            if (result.IsSuccessful)
            {
                PreviewLabel.Text = result.Output?.ToString() ?? "(vacío)";
                ShowMessage("Vista previa generada.");
            }
            else
            {
                PreviewLabel.Text = "ERRORES:\n" + string.Join("\n", result.Errors);
                ShowMessage("Error al generar preview.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MULTA] Error: {ex.Message}\n{ex.StackTrace}");
            PreviewLabel.Text = $"Error: {ex.Message}";
            ShowMessage("Excepción al generar preview.");
        }
    }

    // ─── Imprimir ESC/POS ───

    private async void OnImprimirClicked(object? sender, EventArgs e)
    {
        MessageLabel.Text = "Iniciando impresión...";
        var doc = GetSelectedDocument();
        if (doc == null) return;

        try
        {
            // ── 1. Render SIEMPRE primero (diagnóstico independiente de impresora) ──
            ShowMessage("Generando ESC/POS...");
            var profile = new DeviceProfile("58HB6", 32, "escpos-bitmap");
            profile.SetCapability("supports_bitmap", true);
            profile.SetCapability("bitmap_max_width_px", 320);
            profile.SetCapability("bitmap_binarization_threshold", 128);
            var result = _engine.Render(doc.Value.Template, doc.Value.Data, profile);

            if (!result.IsSuccessful || result.Output is not byte[] bytes)
            {
                var firstErr = result.Errors.FirstOrDefault() ?? "sin errores";
                var snippet = firstErr.Length > 200 ? firstErr[..200] : firstErr;
                MessageLabel.Text = $"{DateTime.Now:HH:mm:ss} RENDER FALLÓ:\n{snippet}";
                System.Console.WriteLine($"[MULTA-RENDER-ERROR] {firstErr}");
                foreach (var w in result.Warnings)
                    System.Console.WriteLine($"[MULTA-WARN] {w}");
                return;
            }

            ShowMessage($"Render OK — {bytes.Length} bytes");
            System.Console.WriteLine($"[MULTA] Render OK: {bytes.Length} bytes");

            // ── 2. Verificar impresora antes de enviar ──
            if (!_printer.IsConnected)
            {
                ShowMessage($"Render OK ({bytes.Length} bytes) pero no hay impresora conectada.");
                return;
            }

            await Task.Delay(500);
            await _printer.SendBytesAsync(bytes);
            ShowMessage($"Impreso OK — {bytes.Length} bytes enviados.");
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException;
            var msg = $"TIPO: {ex.GetType().Name}\n" +
                      $"MSG: {ex.Message}\n" +
                      $"INNER: {inner?.GetType().Name}: {inner?.Message}\n" +
                      $"INNER2: {inner?.InnerException?.Message}\n" +
                      $"STACK: {ex.StackTrace?.Replace("\n", " | ")?.Substring(0, Math.Min(200, ex.StackTrace?.Length ?? 0))}";
            MessageLabel.Text = $"{DateTime.Now:HH:mm:ss} — {msg}";
            System.Console.WriteLine($"[MULTA-ERROR] {msg}");
        }
    }

    // ─── Ver PDF ───

#if ANDROID
    private async void OnVerPdfClicked(object? sender, EventArgs e)
    {
        try
        {
            var doc = GetSelectedDocument();
            if (doc == null)
            {
                ShowMessage("Seleccioná un documento primero.");
                return;
            }
            var profile = new DeviceProfile("pdf", 48, "pdf");
            var result = _engine.Render(doc.Value.Template, doc.Value.Data, profile);
            if (result.IsSuccessful && result.Output is byte[] pdfBytes)
            {
                var path = Path.Combine(FileSystem.CacheDirectory, "multa.pdf");
                File.WriteAllBytes(path, pdfBytes);
                await Launcher.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(path)
                });
                ShowMessage($"PDF generado y abierto: {path}");
            }
            else
            {
                ShowMessage("Error PDF: " + string.Join("; ", result.Errors));
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"Error PDF: {ex.Message}");
        }
    }
#endif

    // ─── Helper ───

    private void ShowMessage(string msg)
    {
        MessageLabel.Text = $"{DateTime.Now:HH:mm:ss} — {msg}";
    }
}
