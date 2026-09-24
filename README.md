# 🖨️ MotorDsl — Motor de generación de documentos térmicos para .NET MAUI

Sistema de generación y renderizado de documentos basado en un DSL JSON, con
foco en **impresión térmica ESC/POS por Bluetooth** desde aplicaciones .NET
MAUI. Construido sobre **.NET 10**, modular, extensible vía transports y
renderers.

---

## ⚡ TL;DR

**Para implementadores:**

```bash
dotnet add package MotorDsl.Maui
dotnet add package MotorDsl.Bluetooth   # impresión Bluetooth — solo Android
dotnet add package MotorDsl.Network     # impresión por red TCP — Android e iOS
```

`MotorDsl.Maui` trae como dependencias transitivas
`MotorDsl.Printing.Abstractions`, `MotorDsl.Core`, `MotorDsl.Rendering` y
`MotorDsl.Extensions`.

Elegí el transport según el medio por el que llega el ticket a la impresora.
`MotorDsl.Bluetooth` cubre las térmicas Classic SPP, que es el parque habitual,
pero **solo funciona en Android**: Apple no expone ese perfil a apps de terceros.
`MotorDsl.Network` habla con impresoras de red por TCP y funciona en las dos
plataformas, así que es el camino para imprimir desde iOS. Los dos pueden
registrarse a la vez y conviven: el servicio rutea por `Kind`.

---

## 📦 Paquetes NuGet

| Paquete | TFM | Rol | Dependencias |
|---|---|---|---|
| `MotorDsl.Core` | net10.0 | Núcleo: contratos, modelos, evaluador, layout | — |
| `MotorDsl.Parser` | net10.0 | Parser DSL JSON → AST | Core |
| `MotorDsl.Rendering` | net10.0 | Renderers Text + EscPos básicos | Core |
| `MotorDsl.Extensions` | net10.0 | Fluent DI: `AddMotorDslEngine`, `AddTemplates`, `AddProfiles`, `AddRenderer` | Core, Parser, Rendering |
| `MotorDsl.Printing.Abstractions` | net10.0 | Contratos transport-agnostic (`IThermalPrinterTransport`, `IThermalPrinterService`, `PrinterDevice`) y orquestador con retry/eventos | Core |
| `MotorDsl.Network` | net10.0 | Transport de red TCP 9100 (RAW / JetDirect). Multiplataforma: Android, iOS y escritorio | Printing.Abstractions |
| `MotorDsl.Bluetooth` | net10.0-android;net10.0-ios | Transport BT Classic SPP (Android). iOS lanza `PlatformNotSupportedException` | Printing.Abstractions |
| `MotorDsl.Maui` | net10.0-android;net10.0-ios | Controles MAUI (`PrinterStatusBadge`, `PrinterPickerView`, `MauiRasterPreview`, `MauiDocumentPreview`, `MauiDiagnosticsView`), renderers (PDF, ESC/POS bitmap, raster preview), `MauiPrintErrorHandler` y diagnóstico (`MauiDiagnosticsReportProvider`) | Core, Rendering, Extensions, Printing.Abstractions |

---

## 🚀 Quickstart .NET MAUI

### 1. Instalación (en tu app MAUI)

```xml
<ItemGroup>
  <PackageReference Include="MotorDsl.Maui"      Version="<latest>" />
  <PackageReference Include="MotorDsl.Bluetooth" Version="<latest>" />
</ItemGroup>
```

### 2. Configurar en `MauiProgram.cs`

```csharp
using Microsoft.Extensions.Logging;
using MotorDsl.Bluetooth;
using MotorDsl.Core.Models;
using MotorDsl.Extensions;
using MotorDsl.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Motor DSL: pipeline + templates + profiles + renderers MAUI
        builder.Services.AddMotorDslEngine()
            .AddTemplates(t =>
            {
                t.Add("acta-infraccion-integrada", MultaIntegratedDsl.Document);
            })
            .AddProfiles(p =>
            {
                p.Add(new DeviceProfile("thermal_58mm", 32, "escpos-bitmap"));
                p.Add(new DeviceProfile("preview", 32, "raster-preview"));
                p.Add(new DeviceProfile("a4-pdf", 80, "pdf"));
            })
            .AddMotorDslMaui();

        // Transport Bluetooth Classic SPP: solo Android. En iOS lanza
        // PlatformNotSupportedException, así que no se registra ahí.
#if ANDROID
        builder.Services.AddBluetoothPrinterTransport();
#endif

        // Transport de red: funciona en Android y en iOS.
        builder.Services.AddNetworkPrinterTransport(o => o
            .AddPrinter("192.168.1.50"));       // puerto 9100 por defecto

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
```

### 3. Permisos Android (`Platforms/Android/AndroidManifest.xml`)

Para `MotorDsl.Bluetooth`:

```xml
<uses-permission android:name="android.permission.BLUETOOTH_SCAN"
                 android:usesPermissionFlags="neverForLocation" />
<uses-permission android:name="android.permission.BLUETOOTH_CONNECT" />
<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
```

Para `MotorDsl.Network`:

```xml
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
```

### 4. Permisos iOS (`Platforms/iOS/Info.plist`)

`MotorDsl.Network` accede a la red local, y desde iOS 14 eso requiere declarar el
motivo. **Sin esta clave el sistema termina la app sin un mensaje de error útil**:

```xml
<key>NSLocalNetworkUsageDescription</key>
<string>Se usa para enviar los documentos a la impresora térmica de la red local.</string>
```

`MotorDsl.Bluetooth` no necesita ninguna clave en iOS porque no funciona ahí.

### 4. UI declarativa con los componentes incluidos

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:muic="clr-namespace:MotorDsl.Maui.Controls;assembly=MotorDsl.Maui">
    <VerticalStackLayout Padding="16" Spacing="12">
        <muic:PrinterStatusBadge x:Name="StatusBadge" />
        <muic:PrinterPickerView  x:Name="DevicePicker" FilterKind="bluetooth" />
        <muic:MauiRasterPreview  x:Name="RasterPreview" ZoomFactor="2" />
        <Button Text="Imprimir" Clicked="OnImprimir" />
    </VerticalStackLayout>
</ContentPage>
```

### 5. Code-behind

```csharp
using MotorDsl.Core.Contracts;
using MotorDsl.Core.Models;
using MotorDsl.Printing;

public partial class MainPage : ContentPage
{
    private readonly IDocumentEngine _engine;
    private readonly IThermalPrinterService _printer;

    public MainPage(IDocumentEngine engine, IThermalPrinterService printer)
    {
        InitializeComponent();
        _engine  = engine;
        _printer = printer;
        StatusBadge.Service  = _printer;
        DevicePicker.Service = _printer;
    }

    private async void OnImprimir(object? sender, EventArgs e)
    {
        var profile = new DeviceProfile("thermal_58mm", 32, "escpos-bitmap");
        var result  = _engine.Render(json, profile);
        if (result.IsSuccessful && _printer.IsConnected)
            await _printer.SendBytesAsync((byte[])result.Output!);
    }
}
```

### 6. Diagnóstico y reporte de fallos

`AddMotorDslMaui()` también registra un `IDiagnosticsReportProvider` que captura
versiones de librería, info de app, dispositivo, impresora y permisos. El
reporte se puede ver en pantalla, imprimir como ticket térmico (con QR de
correlación) o compartir por email/WhatsApp/clipboard vía Share API. Ver
[`diagnostics.md`](https://github.com/Aplicada-Streaming/PrintThermal_Motor_Maui/blob/main/docs/10_developer_guide/diagnostics.md)
para el patrón completo de los 3 botones.

---

## 🏗️ Arquitectura (resumen)

```text
[DSL JSON] → Parser → Evaluator → Layout → Renderer → Output
                                              ↓
                                    Transport (Bluetooth / USB / Red)
                                              ↓
                                          Impresora física
```

- **Pipeline DSL**: JSON → AST → modelo evaluado → modelo con layout → bytes
  finales (texto, ESC/POS, PDF, raster preview).
- **Renderers** son `IRenderer` con un `Target` string. El profile elige qué
  renderer usar.
- **Transports** son `IThermalPrinterTransport` con un `Kind` string. El
  servicio enruta el `PrinterDevice` al transport correcto. **Extensible a
  USB / Red / BLE** sin tocar el orquestador.

📄 Detalles:
- [Arquitectura completa](https://github.com/Aplicada-Streaming/PrintThermal_Motor_Maui/blob/main/docs/05_arquitectura_tecnica/arquitectura-solucion_v1.1.md)
- [Extensibilidad y transports custom](https://github.com/Aplicada-Streaming/PrintThermal_Motor_Maui/blob/main/docs/05_arquitectura_tecnica/extensibilidad-motor_v1.1.md)
- [Guía de integración MAUI](https://github.com/Aplicada-Streaming/PrintThermal_Motor_Maui/blob/main/docs/10_developer_guide/guia-integracion-maui.md)
- [Componentes UX MAUI](https://github.com/Aplicada-Streaming/PrintThermal_Motor_Maui/blob/main/docs/10_developer_guide/componentes-ux-maui.md)
- [Render pixelado y PDF](https://github.com/Aplicada-Streaming/PrintThermal_Motor_Maui/blob/main/docs/10_developer_guide/render-pixelado-y-pdf.md)
- [Compatibilidad de plataformas](https://github.com/Aplicada-Streaming/PrintThermal_Motor_Maui/blob/main/docs/00_contexto/compatibilidad-plataformas_v1.1.md)

---

## 📂 Estructura del repositorio

```text
PrintThermal_Motor_Maui/
├── src/
│   ├── MotorDsl.Core/                    Núcleo del motor (modelos, contratos, evaluador, layout)
│   ├── MotorDsl.Parser/                  Parser DSL JSON
│   ├── MotorDsl.Rendering/               Renderers texto + ESC/POS
│   ├── MotorDsl.Extensions/              Fluent DI (AddMotorDslEngine)
│   ├── MotorDsl.Printing.Abstractions/   Contratos de transport + orquestador
│   ├── MotorDsl.Network/                 Transport de red TCP 9100 (Android + iOS)
│   ├── MotorDsl.Bluetooth/               Transport BT Classic SPP (Android)
│   ├── MotorDsl.Maui/                    Controles + renderers + error handler MAUI
│   └── MotorDsl.Tests/                   Tests del motor
├── samples/
│   ├── MotorDsl.SampleApp/                       Sample mínimo (sin BT)
│   ├── MotorDsl.MultaApp/                        Sample multa con servicios locales
│   ├── MotorDsl.Integrated.MultaApp/             Sample con DSL integrado (datos en plantilla)
│   ├── MotorDsl.Nuget.MultaApp/                  Sample que valida los paquetes NuGet
│   └── MotorDsl.Nuget.Integrated.MultaApp/       Sample integrado vía NuGet
├── docs/                                  Documentación funcional, técnica, sprints, ejemplos
├── scripts/
│   ├── local/                            Scripts dotnet build local
│   ├── local-devcontainer/               Ídem desde Linux, en contenedor efímero (APKs en OUTPUTs/)
│   ├── mobile/                           Publicación APK Android
│   └── nuget/                            Publicación a nuget.org
└── nupkg/                                 .nupkg generados (gitignored)
```

---

## 🧪 Samples disponibles

| Sample | Qué demuestra |
|---|---|
| [MotorDsl.SampleApp](https://github.com/Aplicada-Streaming/PrintThermal_Motor_Maui/tree/main/samples/MotorDsl.SampleApp) | Demo mínimo: render a texto y ESC/POS sin transport. |
| [MotorDsl.MultaApp](https://github.com/Aplicada-Streaming/PrintThermal_Motor_Maui/tree/main/samples/MotorDsl.MultaApp) | Sample completo de multa de tránsito con servicios locales. |
| [MotorDsl.Integrated.MultaApp](https://github.com/Aplicada-Streaming/PrintThermal_Motor_Maui/tree/main/samples/MotorDsl.Integrated.MultaApp) | Sample con DSL en formato "integrated" (datos pre-resueltos). |
| [MotorDsl.Nuget.MultaApp](https://github.com/Aplicada-Streaming/PrintThermal_Motor_Maui/tree/main/samples/MotorDsl.Nuget.MultaApp) | Equivalente a MultaApp pero consumiendo los paquetes NuGet. |
| [MotorDsl.Nuget.Integrated.MultaApp](https://github.com/Aplicada-Streaming/PrintThermal_Motor_Maui/tree/main/samples/MotorDsl.Nuget.Integrated.MultaApp) | Equivalente al Integrated pero vía NuGet. |

Detalles en [`samples/Readme.md`](https://github.com/Aplicada-Streaming/PrintThermal_Motor_Maui/blob/main/samples/Readme.md).

---

## 🛠️ Cómo compilar y correr

### Prerrequisitos

- .NET 10 SDK
- Workload `maui` instalado: `dotnet workload install maui`
- Android SDK (para correr samples en Android)

### Build local

```bash
dotnet build src/MotorDsl.Maui/MotorDsl.Maui.csproj -c Debug
```

### Correr un sample en Android

```bash
dotnet build -t:Run -f net10.0-android samples/MotorDsl.Nuget.Integrated.MultaApp/MotorDsl.Nuget.Integrated.MultaApp.csproj
```

Desde Linux sin instalar el SDK de Android, solo con Docker:

```bash
scripts/local-devcontainer/run-MotorDsl.Nuget.Integrated.MultaApp.sh
```

Más detalles en
[`scripts/local-devcontainer/Readme.md`](https://github.com/Aplicada-Streaming/PrintThermal_Motor_Maui/blob/main/scripts/local-devcontainer/Readme.md),
[`scripts/local/Readme.md`](https://github.com/Aplicada-Streaming/PrintThermal_Motor_Maui/blob/main/scripts/local/Readme.md)
y en
[`scripts/mobile/Readme.md`](https://github.com/Aplicada-Streaming/PrintThermal_Motor_Maui/blob/main/scripts/mobile/Readme.md).

---

## 📤 Publicación NuGet

Los 8 paquetes se publican unificados con la misma versión vía:

```bash
scripts/nuget/publish-motordsl-nuget.bat
```

Detalles en
[`scripts/nuget/notas.md`](https://github.com/Aplicada-Streaming/PrintThermal_Motor_Maui/blob/main/scripts/nuget/notas.md).

---

## 🗺️ Roadmap

- ✅ Transport Bluetooth Classic SPP (Android)
- ✅ Transport de red TCP 9100 — habilita impresión en iOS
- ⏳ Transport USB
- ⏳ Transport BLE
- ⏳ Descubrimiento de impresoras de red por mDNS / Bonjour
- ⏳ Renderer raster con nearest-neighbor real (SKCanvasView)
- ⏳ EAN-13 nativo en raster preview y PDF (hoy fallback a texto)

---

## 📜 Licencia

MIT.

---

## 🤝 Contribución y contacto

Fernando Rafael Filipuzzi — [Aplicada Streaming 2026](https://github.com/Aplicada-Streaming)
