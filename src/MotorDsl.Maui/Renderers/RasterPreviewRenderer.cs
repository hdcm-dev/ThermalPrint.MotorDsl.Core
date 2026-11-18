using System.Linq;
using MotorDsl.Core.Contracts;
using MotorDsl.Core.Models;
using SkiaSharp;

namespace MotorDsl.Maui.Renderers;

/// <summary>
/// Renderer que produce un PNG simulando lo que la impresora térmica
/// rasterizaría (texto monoespaciado 1-bit, bitmaps, QR y barcode-fallback).
/// Útil para vista previa pixelada sin enviar al hardware.
/// Target: "raster-preview".
/// </summary>
public class RasterPreviewRenderer : IRenderer
{
    private readonly IBitmapRasterizer _rasterizer;
    private readonly QrCodeRasterizer _qrRasterizer;

    public string Target => "raster-preview";

    public RasterPreviewRenderer(IBitmapRasterizer rasterizer, QrCodeRasterizer qrRasterizer)
    {
        _rasterizer = rasterizer;
        _qrRasterizer = qrRasterizer;
    }

    public RenderResult Render(LayoutedDocument document, DeviceProfile profile)
    {
        var result = new RenderResult(Target);
        try
        {
            int canvasWidth = Convert.ToInt32(profile.GetCapability("bitmap_max_width_px") ?? 384);
            int chars = profile.Width > 0 ? profile.Width : 32;
            // Monospace ratio: char width ~= 0.6 * font size  →  fontSize = charWidth / 0.6
            double charPxWidth = (double)canvasWidth / chars;
            int fontSize = Math.Max(10, (int)(charPxWidth / 0.6));
            int lineHeight = (int)(fontSize * 1.4);
            int padding = 16;

            var orderedEntries = (document?.NodeLayoutInfo ?? new Dictionary<string, LayoutInfo>())
                .OrderBy(kvp => kvp.Value.LineNumber)
                .ThenBy(kvp => kvp.Value.ColumnNumber)
                .Select(kvp => kvp.Value)
                .ToList();

            // ── Pase 1: pre-calcular alto total ──
            int totalHeight = padding;
            // Pre-rasterizar bitmaps/QR para reusar en pase 2 sin generar dos veces
            var preRasterized = new Dictionary<int, SKBitmap>();
            for (int i = 0; i < orderedEntries.Count; i++)
            {
                var info = orderedEntries[i];
                if (IsQr(info, out var qrData))
                {
                    var qrBmp = _qrRasterizer.Rasterize(qrData!, moduleSize: 4);
                    preRasterized[i] = qrBmp;
                    totalHeight += qrBmp.Height + 8;
                }
                else if (IsBitmap(info, out var bmpSrc))
                {
                    try
                    {
                        var raster = _rasterizer.Rasterize(bmpSrc!, canvasWidth);
                        var bmp = ConvertRasterToBitmap(raster);
                        preRasterized[i] = bmp;
                        totalHeight += bmp.Height + 8;
                    }
                    catch (Exception ex)
                    {
                        result.AddWarning($"No se pudo rasterizar bitmap en línea {info.LineNumber}: {ex.Message}");
                        totalHeight += lineHeight + 4;
                    }
                }
                else if (IsBarcode(info, out _))
                {
                    totalHeight += lineHeight + 4;
                }
                else if (!string.IsNullOrEmpty(info.WrappedText))
                {
                    // El LayoutEngine une con '\n' las líneas de un texto partido: se reserva
                    // una línea por cada una, no una sola para todo el bloque.
                    totalHeight += lineHeight * SplitLines(info.WrappedText).Length;
                }
            }
            totalHeight += padding;
            if (totalHeight < padding * 2 + lineHeight)
                totalHeight = padding * 2 + lineHeight;

            // ── Pase 2: dibujar ──
            using var bitmap = new SKBitmap(canvasWidth, totalHeight);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);

            using var typeface = LoadEmbeddedTypeface() ?? SKTypeface.FromFamilyName("monospace");
            using var font = new SKFont(typeface, fontSize);
            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = false,
                Style = SKPaintStyle.Fill
            };

            int y = padding;
            for (int i = 0; i < orderedEntries.Count; i++)
            {
                var info = orderedEntries[i];

                if (IsQr(info, out _) && preRasterized.TryGetValue(i, out var qrBmp))
                {
                    int x = AlignmentToX(info.Alignment, canvasWidth, qrBmp.Width);
                    canvas.DrawBitmap(qrBmp, x, y);
                    y += qrBmp.Height + 8;
                }
                else if (IsBitmap(info, out _) && preRasterized.TryGetValue(i, out var imgBmp))
                {
                    int x = AlignmentToX(info.Alignment, canvasWidth, imgBmp.Width);
                    canvas.DrawBitmap(imgBmp, x, y);
                    y += imgBmp.Height + 8;
                }
                else if (IsBarcode(info, out var bcData))
                {
                    var label = $"[Código: {bcData}]";
                    DrawText(canvas, label, info.Alignment, canvasWidth, y, font, paint);
                    y += lineHeight + 4;
                }
                else if (!string.IsNullOrEmpty(info.WrappedText))
                {
                    bool bold = info.DeviceMetadata.TryGetValue("bold", out var bv) && bv is true;
                    using var boldTf = bold
                        ? SKTypeface.FromFamilyName(typeface?.FamilyName ?? "monospace", SKFontStyle.Bold)
                        : null;
                    using var boldFont = bold ? new SKFont(boldTf ?? typeface, fontSize) : null;
                    var lineFont = boldFont ?? font;

                    // Una llamada a DrawText por línea: SkiaSharp no interpreta '\n' (lo dibuja
                    // como glifo faltante y sigue en la misma línea). La alineación se calcula
                    // por línea, con el ancho de cada una.
                    foreach (var line in SplitLines(info.WrappedText))
                    {
                        if (line.Length > 0)
                            DrawText(canvas, line, info.Alignment, canvasWidth, y, lineFont, paint);
                        y += lineHeight;
                    }
                }
            }

            // Encode to PNG
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            result.Output = data.ToArray();

            // Cleanup pre-rasterized bitmaps
            foreach (var bmp in preRasterized.Values)
                bmp.Dispose();
        }
        catch (Exception ex)
        {
            result.AddError($"RasterPreview error: {ex.Message}");
            result.Output = Array.Empty<byte>();
        }
        return result;
    }

    private static bool IsQr(LayoutInfo info, out string? data)
    {
        if (info.DeviceMetadata.TryGetValue("is_qr", out var flag) && flag is true
            && info.DeviceMetadata.TryGetValue("qr_data", out var d) && d is string s && !string.IsNullOrEmpty(s))
        {
            data = s;
            return true;
        }
        data = null;
        return false;
    }

    private static bool IsBitmap(LayoutInfo info, out string? source)
    {
        if (info.DeviceMetadata.TryGetValue("is_bitmap", out var flag) && flag is true
            && info.DeviceMetadata.TryGetValue("bitmap_source", out var s) && s is string str && !string.IsNullOrEmpty(str))
        {
            source = str;
            return true;
        }
        source = null;
        return false;
    }

    private static bool IsBarcode(LayoutInfo info, out string? data)
    {
        if (info.DeviceMetadata.TryGetValue("is_barcode", out var flag) && flag is true
            && info.DeviceMetadata.TryGetValue("barcode_data", out var d))
        {
            data = d?.ToString() ?? "";
            return true;
        }
        data = null;
        return false;
    }

    private static int AlignmentToX(string? alignment, int canvasWidth, int contentWidth) => alignment?.ToLower() switch
    {
        "center" => Math.Max(0, (canvasWidth - contentWidth) / 2),
        "right" => Math.Max(0, canvasWidth - contentWidth - 8),
        _ => 8
    };

    /// <summary>
    /// Parte el <c>WrappedText</c> del layout en sus líneas (el motor las une con <c>'\n'</c>;
    /// se acepta también <c>"\r\n"</c>). Las líneas vacías se conservan: ocupan su alto.
    /// </summary>
    internal static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private static void DrawText(SKCanvas canvas, string text, string? alignment, int canvasWidth, int y, SKFont font, SKPaint paint)
    {
        float textWidth = font.MeasureText(text);
        int x = AlignmentToX(alignment, canvasWidth, (int)Math.Ceiling(textWidth));
        // baseline offset: y + size para que la línea base quede dentro del lineHeight
        canvas.DrawText(text, x, y + font.Size, font, paint);
    }

    /// <summary>
    /// Convierte el bit array packed de RasterizedImage a un SKBitmap blanco/negro.
    /// </summary>
    private static SKBitmap ConvertRasterToBitmap(RasterizedImage raster)
    {
        int widthPx = raster.WidthBytes * 8;
        var bmp = new SKBitmap(widthPx, raster.HeightDots, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (int row = 0; row < raster.HeightDots; row++)
        {
            for (int col = 0; col < widthPx; col++)
            {
                int byteIndex = row * raster.WidthBytes + (col / 8);
                int bitIndex = 7 - (col % 8);
                bool on = (raster.Bits[byteIndex] & (1 << bitIndex)) != 0;
                bmp.SetPixel(col, row, on ? SKColors.Black : SKColors.White);
            }
        }
        return bmp;
    }

    private static SKTypeface? LoadEmbeddedTypeface()
    {
        try
        {
            var assembly = typeof(RasterPreviewRenderer).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("DroidSans-Regular.ttf"));
            if (resourceName == null) return null;
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return null;
            // SKTypeface.FromStream consume el stream; copiamos a memoria primero.
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            return SKTypeface.FromStream(ms);
        }
        catch
        {
            return null;
        }
    }
}
