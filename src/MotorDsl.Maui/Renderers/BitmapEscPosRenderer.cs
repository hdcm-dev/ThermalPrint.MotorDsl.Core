using MotorDsl.Core.Contracts;
using MotorDsl.Core.Models;
using MotorDsl.Rendering;
using System.Text;

namespace MotorDsl.Maui.Renderers;

/// <summary>
/// Renderer ESC/POS extendido con soporte de imágenes bitmap rasterizadas.
/// Para nodos con is_bitmap=true, usa IBitmapRasterizer → GS v 0.
/// Para el resto, delega al EscPosRenderer base.
/// El texto se codifica con la misma tabla que EscPosRenderer (PC437 por defecto, configurable en
/// el perfil): ver <see cref="EscPosTextEncoding"/>. <c>ESC t</c> se emite sólo si algún texto
/// lleva caracteres fuera de ASCII, de modo que un ticket ASCII sale byte a byte como antes.
/// Sprint 08 | TK-64
/// </summary>
public class BitmapEscPosRenderer : IRenderer
{
    private readonly IBitmapRasterizer _rasterizer;
    private readonly EscPosRenderer _baseRenderer = new();

    public string Target => "escpos-bitmap";

    public BitmapEscPosRenderer(IBitmapRasterizer rasterizer)
    {
        _rasterizer = rasterizer;
    }

    public RenderResult Render(LayoutedDocument document, DeviceProfile profile)
    {
        try
        {
            var result = new RenderResult(Target);
            var buffer = new List<byte>();

            buffer.AddRange(EscPosCommands.Init);

            if (document?.Root != null)
            {
                var entries = document.NodeLayoutInfo
                    .Where(kvp => !string.IsNullOrEmpty(kvp.Value.WrappedText))
                    .OrderBy(kvp => kvp.Value.LineNumber)
                    .ThenBy(kvp => kvp.Value.ColumnNumber)
                    .ToList();

                // Tabla de caracteres: la misma página para ESC t y para codificar el texto. Las
                // vocales con acento, la ñ, ° ¿ ¡ salían como «?» porque el texto iba en ASCII.
                // ESC t sólo hace falta si se va a mandar un byte >= 0x80: el ASCII es igual en
                // todas las tablas, y así un ticket ASCII no cambia.
                var textEncoding = EscPosTextEncoding.For(profile);
                if (entries.Any(kvp => IsText(kvp.Value) && !EscPosTextEncoding.IsAscii(kvp.Value.WrappedText)))
                    buffer.AddRange(textEncoding.SelectCommand);

                foreach (var kvp in entries)
                {
                    var layoutInfo = kvp.Value;

                    // Bitmap image → rasterize and emit GS v 0
                    if (layoutInfo.DeviceMetadata.TryGetValue("is_bitmap", out var bmpFlag) && bmpFlag is true)
                    {
                        var source = layoutInfo.DeviceMetadata.TryGetValue("bitmap_source", out var src)
                            ? src?.ToString() ?? ""
                            : "";
                        int maxW = Convert.ToInt32(profile.GetCapability("bitmap_max_width_px") ?? 384);
                        var widthPixels = layoutInfo.DeviceMetadata.TryGetValue("bitmap_width", out var w)
                            ? Math.Min(Convert.ToInt32(w) * 8, maxW)
                            : maxW;

                        // Recorte del source para los mensajes (evita volcar todo el base64).
                        var sourcePreview = source.Length > 40 ? source[..40] + "…" : source;

                        // Fail-loud, pero con contexto: si la imagen no se puede rasterizar,
                        // se aborta el ticket con un Error PRECISO (qué imagen y por qué), en
                        // lugar del genérico "BitmapEscPos rendering failed" del catch externo.
                        RasterizedImage rasterized;
                        try
                        {
                            rasterized = _rasterizer.Rasterize(source, widthPixels);
                        }
                        catch (Exception imgEx)
                        {
                            result.AddError($"Image rasterization failed (source='{sourcePreview}'): {imgEx.Message}");
                            result.Output = Array.Empty<byte>();
                            return result;
                        }

                        // Warning (no aborta): la imagen decodificó pero salió toda en blanco
                        // (típico de placeholders transparentes). El ticket se imprime igual,
                        // pero avisamos porque esa imagen no se va a ver.
                        if (rasterized.Bits == null || rasterized.Bits.Length == 0 || rasterized.Bits.All(b => b == 0))
                            result.AddWarning($"Imagen rasterizada en blanco (posible placeholder transparente): source='{sourcePreview}'");

                        buffer.AddRange(EmitGsV0(rasterized));
                        buffer.AddRange(EscPosCommands.LineFeed);
                        continue;
                    }

                    // QR code
                    if (layoutInfo.DeviceMetadata.TryGetValue("is_qr", out var qrFlag) && qrFlag is true)
                    {
                        var qrData = layoutInfo.DeviceMetadata["qr_data"]?.ToString() ?? "";
                        buffer.AddRange(EmitQrCode(qrData));
                        continue;
                    }

                    // Barcode
                    if (layoutInfo.DeviceMetadata.TryGetValue("is_barcode", out var bcFlag) && bcFlag is true)
                    {
                        var barcodeData = layoutInfo.DeviceMetadata["barcode_data"]?.ToString() ?? "";
                        buffer.AddRange(EmitBarcodeEan13(barcodeData));
                        continue;
                    }

                    // Text — alignment, bold, content
                    buffer.AddRange(GetAlignmentCommand(layoutInfo.Alignment));

                    bool isBold = layoutInfo.DeviceMetadata.TryGetValue("bold", out var bv) && bv is true;
                    if (isBold) buffer.AddRange(EscPosCommands.StyleBold);

                    buffer.AddRange(textEncoding.Encode(layoutInfo.WrappedText));

                    if (isBold) buffer.AddRange(EscPosCommands.StyleNormal);

                    buffer.AddRange(EscPosCommands.LineFeed);
                }
            }

            // Cola del ticket segun el perfil (default: corte parcial, NO corte total: ver
            // EscPosCommands.EndOfTicket).
            buffer.AddRange(EscPosCommands.EndOfTicket(
                profile.GetCapability("end_of_ticket") as string,
                Convert.ToInt32(profile.GetCapability("end_feed_lines") ?? 4)));

            result.Output = buffer.ToArray();
            return result;
        }
        catch (Exception ex)
        {
            var result = new RenderResult(Target);
            result.AddError($"BitmapEscPos rendering failed: {ex.Message}");
            result.Output = Array.Empty<byte>();
            return result;
        }
    }

    /// <summary>True si la entrada se emite como texto (no es bitmap, QR ni código de barras).</summary>
    private static bool IsText(LayoutInfo info) =>
        !(info.DeviceMetadata.TryGetValue("is_bitmap", out var b) && b is true) &&
        !(info.DeviceMetadata.TryGetValue("is_qr", out var q) && q is true) &&
        !(info.DeviceMetadata.TryGetValue("is_barcode", out var c) && c is true);

    /// <summary>
    /// GS v 0 — Print raster bit image.
    /// Command: 1D 76 30 00 xL xH yL yH [data]
    /// </summary>
    private static byte[] EmitGsV0(RasterizedImage raster)
    {
        var buffer = new List<byte>();
        byte xL = (byte)(raster.WidthBytes & 0xFF);
        byte xH = (byte)((raster.WidthBytes >> 8) & 0xFF);
        byte yL = (byte)(raster.HeightDots & 0xFF);
        byte yH = (byte)((raster.HeightDots >> 8) & 0xFF);

        buffer.AddRange(new byte[] { 0x1D, 0x76, 0x30, 0x00, xL, xH, yL, yH });
        buffer.AddRange(raster.Bits);
        return buffer.ToArray();
    }

    private static byte[] EmitQrCode(string data)
    {
        var buffer = new List<byte>();
        var urlBytes = Encoding.ASCII.GetBytes(data);
        int len = urlBytes.Length + 3;
        byte ll = (byte)(len & 0xFF);
        byte lh = (byte)((len >> 8) & 0xFF);

        buffer.AddRange(EscPosCommands.QrSetSize3);
        buffer.AddRange(EscPosCommands.QrSetErrorM);
        buffer.AddRange(new byte[] { 0x1D, 0x28, 0x6B, ll, lh, 0x31, 0x50, 0x30 });
        buffer.AddRange(urlBytes);
        buffer.AddRange(EscPosCommands.QrPrint);
        return buffer.ToArray();
    }

    private static byte[] EmitBarcodeEan13(string data)
    {
        var buffer = new List<byte>();
        buffer.AddRange(EscPosCommands.BarcodeEan13);
        buffer.AddRange(Encoding.ASCII.GetBytes(data));
        buffer.Add(0x00);
        return buffer.ToArray();
    }

    private static byte[] GetAlignmentCommand(string? alignment) => alignment?.ToLower() switch
    {
        "center" => EscPosCommands.AlignCenter,
        "right" => EscPosCommands.AlignRight,
        _ => EscPosCommands.AlignLeft
    };
}
