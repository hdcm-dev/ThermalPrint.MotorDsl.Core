using MotorDsl.Core.Contracts;
using MotorDsl.Core.Models;
using System.Text;

namespace MotorDsl.Rendering;

/// <summary>
/// Renderer that generates ESC/POS byte[] commands for thermal printers.
/// No external dependencies (no ESCPOS_NET).
/// Sprint 04 | TK-20
/// Sprint 08 | TK-64 — soporte opcional de bitmap vía IBitmapRasterizer
/// </summary>
public class EscPosRenderer : IRenderer
{
    private readonly IBitmapRasterizer? _rasterizer;

    public EscPosRenderer(IBitmapRasterizer? rasterizer = null)
    {
        _rasterizer = rasterizer;
    }

    public string Target => "escpos";

    public RenderResult Render(LayoutedDocument document, DeviceProfile profile)
    {
        try
        {
            var result = new RenderResult("escpos");
            var buffer = new List<byte>();

            // Tabla de caracteres del perfil (PC437 por defecto): ESC t y la codificación del
            // texto salen de la misma página. Ver EscPosTextEncoding.
            var textEncoding = EscPosTextEncoding.For(profile);

            // Initialize printer
            buffer.AddRange(EscPosCommands.Init);
            // Seleccionar la tabla (ESC t n)
            buffer.AddRange(textEncoding.SelectCommand);

            if (document?.Root != null)
            {
                var entries = document.NodeLayoutInfo
                    .Where(kvp => !string.IsNullOrEmpty(kvp.Value.WrappedText))
                    .OrderBy(kvp => kvp.Value.LineNumber)
                    .ThenBy(kvp => kvp.Value.ColumnNumber);

                foreach (var kvp in entries)
                {
                    var layoutInfo = kvp.Value;

                    // QR code — emit GS ( k sequence instead of text
                    if (layoutInfo.DeviceMetadata.TryGetValue("is_qr", out var qrFlag) && qrFlag is true)
                    {
                        var qrData = layoutInfo.DeviceMetadata["qr_data"]?.ToString() ?? "";
                        buffer.AddRange(EmitQrCode(qrData, GetEncoding(profile)));
                        continue;
                    }

                    // Barcode EAN-13 — emit GS k 2 sequence
                    if (layoutInfo.DeviceMetadata.TryGetValue("is_barcode", out var bcFlag) && bcFlag is true)
                    {
                        var barcodeData = layoutInfo.DeviceMetadata["barcode_data"]?.ToString() ?? "";
                        buffer.AddRange(EmitBarcodeEan13(barcodeData, GetEncoding(profile)));
                        continue;
                    }

                    // Bitmap image — ruteo por rol (logo NV recall / inline). El renderer NO conoce
                    // el transport ni las capabilities: solo lee del DeviceProfile lo que la app seteo.
                    if (layoutInfo.DeviceMetadata.TryGetValue("is_bitmap", out var bmpFlag) && bmpFlag is true)
                    {
                        var source = layoutInfo.DeviceMetadata.TryGetValue("bitmap_source", out var src)
                            ? src?.ToString() ?? ""
                            : "";
                        var role = layoutInfo.DeviceMetadata.TryGetValue("image_role", out var rv)
                            ? rv?.ToString()
                            : null;

                        // Logo + profile con nv_logo_keycode -> recall NV (pocos bytes), no se rasteriza.
                        // Las firmas (signature) y cualquier otra imagen van SIEMPRE inline (GS v 0).
                        if (role == "logo" && profile.GetCapability("nv_logo_keycode") != null)
                        {
                            EmitNvLogoRecall(buffer, profile);
                            continue;
                        }

                        EmitBitmap(buffer, source, profile, _rasterizer);
                        continue;
                    }

                    // Emit alignment command
                    buffer.AddRange(GetAlignmentCommand(layoutInfo.Alignment));

                    // Bold on if metadata says so
                    bool isBold = layoutInfo.DeviceMetadata.TryGetValue("bold", out var bv) && bv is true;
                    if (isBold)
                        buffer.AddRange(EscPosCommands.StyleBold);

                    // Texto codificado con la página de la tabla seleccionada
                    buffer.AddRange(textEncoding.Encode(layoutInfo.WrappedText));

                    // Bold off after bold line
                    if (isBold)
                        buffer.AddRange(EscPosCommands.StyleNormal);

                    // Line feed after each text entry
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
            var result = new RenderResult("escpos");
            result.AddError($"ESC/POS rendering failed: {ex.Message}");
            result.Output = Array.Empty<byte>();
            return result;
        }
    }

    private static byte[] GetAlignmentCommand(string? alignment) => alignment?.ToLower() switch
    {
        "center" => EscPosCommands.AlignCenter,
        "right" => EscPosCommands.AlignRight,
        _ => EscPosCommands.AlignLeft
    };

    private static Encoding GetEncoding(DeviceProfile profile)
    {
        var codepage = profile.GetCapability("codepage")?.ToString();
        return codepage switch
        {
            "850" => Encoding.GetEncoding(850),
            "437" => Encoding.GetEncoding(437),
            _ => Encoding.ASCII
        };
    }

    private static byte[] EmitQrCode(string data, Encoding encoding)
    {
        var buffer = new List<byte>();
        var urlBytes = encoding.GetBytes(data);
        int len = urlBytes.Length + 3;
        byte ll = (byte)(len & 0xFF);
        byte lh = (byte)((len >> 8) & 0xFF);

        // Set QR module size
        buffer.AddRange(EscPosCommands.QrSetSize3);
        // Set QR error correction level
        buffer.AddRange(EscPosCommands.QrSetErrorM);
        // Store QR data: GS ( k pL pH 31 50 30 <data>
        buffer.AddRange(new byte[] { 0x1D, 0x28, 0x6B, ll, lh, 0x31, 0x50, 0x30 });
        buffer.AddRange(urlBytes);
        // Print QR code
        buffer.AddRange(EscPosCommands.QrPrint);

        return buffer.ToArray();
    }

    private static byte[] EmitBarcodeEan13(string data, Encoding encoding)
    {
        var buffer = new List<byte>();

        // GS k 2 — EAN-13 barcode (mode 2, NUL-terminated)
        buffer.AddRange(EscPosCommands.BarcodeEan13);
        buffer.AddRange(encoding.GetBytes(data));
        buffer.Add(0x00); // NUL terminator

        return buffer.ToArray();
    }

    /// <summary>
    /// Emite un bitmap como comando GS v 0 (0x1D 0x76 0x30) si hay rasterizador disponible.
    /// Si no hay rasterizador o ocurre un error, emite el placeholder textual "[BITMAP]" + LF.
    /// Sprint 08 | TK-64
    /// </summary>
    private static void EmitBitmap(List<byte> buffer, string base64Source,
        DeviceProfile profile, IBitmapRasterizer? rasterizer)
    {
        try
        {
            // 1. Extraer base64 puro (quitar prefijo data:image/...;base64,)
            var base64 = base64Source.Contains(",")
                ? base64Source.Split(',')[1]
                : base64Source;

            if (string.IsNullOrEmpty(base64) || rasterizer == null)
            {
                // Sin rasterizador: placeholder textual
                buffer.AddRange(Encoding.ASCII.GetBytes("[BITMAP]"));
                buffer.Add(0x0A);
                return;
            }

            // 2. Obtener límites del perfil
            int maxW = Convert.ToInt32(profile.GetCapability("bitmap_max_width_px") ?? 384);

            // 3. Rasterizar: el IBitmapRasterizer adapta ancho, escala, binariza
            var rasterized = rasterizer.Rasterize(base64Source, maxW);

            int bytesPerRow = rasterized.WidthBytes;
            int alto = rasterized.HeightDots;

            // 4. Construir GS v 0 (0x1D 0x76 0x30 0x00 xL xH yL yH datos...) via builder compartido
            buffer.AddRange(EscPosCommands.AlignCenter);
            buffer.AddRange(EscPosCommands.BuildRasterImageGsV0(rasterized.Bits, bytesPerRow, alto));
            buffer.AddRange(EscPosCommands.LineFeed);
            buffer.AddRange(EscPosCommands.AlignLeft);
        }
        catch
        {
            // Fallback: placeholder textual
            buffer.AddRange(Encoding.ASCII.GetBytes("[BITMAP]"));
            buffer.Add(0x0A);
        }
    }

    /// <summary>
    /// Emite el comando de recall del logo NV segun la familia que la app dejo en el profile:
    /// "fs" -> FS p (NV bit image por numero); cualquier otra ("gsl" o no seteada) -> GS ( L
    /// recall por keycode. El renderer no conoce el transport: solo lee nv_logo_keycode y
    /// nv_logo_kind del DeviceProfile.
    /// </summary>
    private static void EmitNvLogoRecall(List<byte> buffer, DeviceProfile profile)
    {
        int keycode = Convert.ToInt32(profile.GetCapability("nv_logo_keycode"));
        var kind = profile.GetCapability("nv_logo_kind")?.ToString();

        buffer.AddRange(EscPosCommands.AlignCenter);
        buffer.AddRange(kind == "fs"
            ? EscPosCommands.RecallNvBitImageFsP(keycode)
            : EscPosCommands.RecallNvGraphicsGsL(keycode));
        buffer.AddRange(EscPosCommands.LineFeed);
        buffer.AddRange(EscPosCommands.AlignLeft);
    }
}
