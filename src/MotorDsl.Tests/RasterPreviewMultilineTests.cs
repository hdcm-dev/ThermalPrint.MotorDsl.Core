using System.Text;
using System.Text.Json;
using MotorDsl.Core.Engine;
using MotorDsl.Core.Evaluators;
using MotorDsl.Core.Layout;
using MotorDsl.Core.Models;
using MotorDsl.Core.Validation;
using MotorDsl.Integrated.MultaApp.Templates;
using MotorDsl.Maui.Renderers;
using MotorDsl.Pax;
using MotorDsl.Parser;
using SkiaSharp;

namespace MotorDsl.Tests;

/// <summary>
/// Texto partido en varias líneas en el PNG de raster-preview (el que recibe la impresora PAX).
/// El LayoutEngine une las líneas de un texto largo con '\n' en <c>WrappedText</c>; el renderer
/// raster tiene que dibujar cada una en su renglón y reservar el alto de todas. Antes las
/// dibujaba en un solo renglón: el '\n' salía como glifo faltante y el resto se cortaba a 384 px.
/// </summary>
public class RasterPreviewMultilineTests
{
    private const string TextoLargo =
        "Causa: sin causa (este tipo de acta no genera una causa en el juzgado de faltas)";

    private static readonly RasterPreviewRenderer Renderer =
        new(new SkiaSharpRasterizer(), new QrCodeRasterizer());

    private static DeviceProfile Perfil() => PaxProfiles.A910S(384);

    private static LayoutedDocument Layout(string integratedJson, DeviceProfile profile)
    {
        var template = new DslParser().Parse(integratedJson);
        var evaluated = new EvaluatedDocument { Id = template.Id, Version = template.Version, Root = template.Root };
        return new LayoutEngine().ApplyLayout(evaluated, profile);
    }

    private static string Documento(IEnumerable<string> textos, string align)
    {
        var children = textos.Select(t => new Dictionary<string, object>
        {
            ["type"] = "text",
            ["value"] = t,
            ["style"] = new Dictionary<string, object> { ["align"] = align },
        });
        return JsonSerializer.Serialize(new
        {
            id = "multilinea",
            version = "1.0",
            format = "integrated",
            root = new { type = "container", layout = "vertical", children },
        });
    }

    private static SKBitmap RenderPng(LayoutedDocument layouted, DeviceProfile profile)
    {
        var result = Renderer.Render(layouted, profile);
        Assert.Empty(result.Errors);
        var png = Assert.IsType<byte[]>(result.Output);
        return SKBitmap.Decode(png) ?? throw new InvalidOperationException("El renderer no devolvió un PNG.");
    }

    private static SKBitmap RenderPng(string integratedJson) => RenderPng(Layout(integratedJson, Perfil()), Perfil());

    private static string[] LineasDelLayout(string integratedJson)
    {
        var info = Layout(integratedJson, Perfil()).NodeLayoutInfo.Values
            .Single(l => !string.IsNullOrEmpty(l.WrappedText));
        return info.WrappedText.Split('\n');
    }

    private static void AssertMismosPixeles(SKBitmap esperado, SKBitmap actual, int filas, string que)
    {
        Assert.Equal(esperado.Width, actual.Width);
        for (int y = 0; y < filas; y++)
            for (int x = 0; x < esperado.Width; x++)
                if (esperado.GetPixel(x, y) != actual.GetPixel(x, y))
                    Assert.Fail($"{que}: el píxel ({x},{y}) difiere.");
    }

    [Fact]
    public void Precondicion_ElLayoutParteElTextoLargoConSaltos()
    {
        var lineas = LineasDelLayout(Documento(new[] { TextoLargo }, "left"));

        Assert.True(lineas.Length >= 3, $"Se esperaban 3 o más líneas, hubo {lineas.Length}.");
        Assert.All(lineas, l => Assert.True(l.Length <= Perfil().Width));
    }

    [Fact]
    public void Render_TextoPartidoEnN_ReservaElAltoDeNLineas()
    {
        var lineas = LineasDelLayout(Documento(new[] { TextoLargo }, "left"));

        using var partido = RenderPng(Documento(new[] { TextoLargo }, "left"));
        using var unaLinea = RenderPng(Documento(new[] { lineas[0] }, "left"));
        using var dosLineas = RenderPng(Documento(new[] { lineas[0], lineas[1] }, "left"));
        int altoDeLinea = dosLineas.Height - unaLinea.Height;

        Assert.True(altoDeLinea > 0);
        Assert.Equal(unaLinea.Height + (lineas.Length - 1) * altoDeLinea, partido.Height);
    }

    [Fact]
    public void Render_TextoPartido_ElPrimerRenglonNoTraeElSaltoNiLaLineaSiguiente()
    {
        var lineas = LineasDelLayout(Documento(new[] { TextoLargo }, "left"));

        using var partido = RenderPng(Documento(new[] { TextoLargo }, "left"));
        using var soloPrimera = RenderPng(Documento(new[] { lineas[0] }, "left"));

        // El renglón de la primera línea (padding superior incluido) es idéntico al de esa línea
        // sola: no hay glifo faltante del '\n' ni palabras de la línea siguiente a su derecha.
        AssertMismosPixeles(soloPrimera, partido, soloPrimera.Height - 16, "Primer renglón");
    }

    [Theory]
    [InlineData("left")]
    [InlineData("center")]
    [InlineData("right")]
    public void Render_TextoPartido_EsIgualAUnNodoPorLinea_ConLaAlineacionDeCadaLinea(string align)
    {
        var lineas = LineasDelLayout(Documento(new[] { TextoLargo }, align));

        using var partido = RenderPng(Documento(new[] { TextoLargo }, align));
        using var porLinea = RenderPng(Documento(lineas, align));

        Assert.Equal(porLinea.Height, partido.Height);
        AssertMismosPixeles(porLinea, partido, porLinea.Height, $"Alineación {align}");
    }

    [Fact]
    public void SplitLines_ConservaLineasVaciasYAceptaCrLf()
    {
        Assert.Equal(new[] { "a", "b" }, RasterPreviewRenderer.SplitLines("a\nb"));
        Assert.Equal(new[] { "a", "", "b" }, RasterPreviewRenderer.SplitLines("a\r\n\nb"));
        Assert.Equal(new[] { "solo" }, RasterPreviewRenderer.SplitLines("solo"));
    }

    [Fact]
    public void Render_ActaIntegrada384px_CadaLineaPartidaSumaSuRenglon()
    {
        var profile = Perfil();

        // El acta completa por el motor, como la imprime el PAX.
        var registry = new RendererRegistry();
        registry.Register(Renderer);
        var engine = new DocumentEngine(new DslParser(), new Evaluator(), new LayoutEngine(), registry,
            new DataValidator(), new TemplateValidator(), new ProfileValidator());
        var result = engine.Render(MultaIntegratedDsl.Document, profile);
        Assert.Empty(result.Errors);
        using var acta = SKBitmap.Decode(Assert.IsType<byte[]>(result.Output));
        Assert.Equal(384, acta.Width);

        // Las mismas líneas, con cada texto partido recortado a su primera línea.
        var layouted = Layout(MultaIntegratedDsl.Document, profile);
        var partidos = layouted.NodeLayoutInfo.Values
            .Where(l => l.WrappedText.Contains('\n') && !l.DeviceMetadata.ContainsKey("is_bitmap")
                        && !l.DeviceMetadata.ContainsKey("is_qr") && !l.DeviceMetadata.ContainsKey("is_barcode"))
            .ToList();
        int lineasExtra = partidos.Sum(l => l.WrappedText.Split('\n').Length - 1);
        Assert.Contains(partidos, l => l.WrappedText.Contains("velocidad"));
        Assert.True(lineasExtra >= 5, $"El acta debería partir varios textos; partió {lineasExtra} líneas extra.");

        foreach (var l in partidos)
            l.WrappedText = l.WrappedText.Split('\n')[0];
        using var recortada = RenderPng(layouted, profile);

        using var unaLinea = RenderPng(Documento(new[] { "x" }, "left"));
        using var dosLineas = RenderPng(Documento(new[] { "x", "x" }, "left"));
        int altoDeLinea = dosLineas.Height - unaLinea.Height;

        Assert.Equal(recortada.Height + lineasExtra * altoDeLinea, acta.Height);
    }

    [Fact]
    public void EscPosBitmap_TextoPartido_SeparaLasLineasConLineFeedYMantieneLaAlineacion()
    {
        // escpos-bitmap (Bluetooth) manda el '\n' como LF (0x0A): la impresora avanza un renglón
        // y ESC a sigue vigente para los siguientes. No comparte el defecto del raster.
        var profile = new DeviceProfile("thermal_58mm", 32, "escpos-bitmap");
        var json = Documento(new[] { "Exceso de velocidad en zona urbana con radar fijo" }, "center");
        var layouted = Layout(json, profile);
        var lineas = layouted.NodeLayoutInfo.Values.Single(l => !string.IsNullOrEmpty(l.WrappedText)).WrappedText.Split('\n');
        Assert.True(lineas.Length >= 2);

        var result = new BitmapEscPosRenderer(new SkiaSharpRasterizer()).Render(layouted, profile);

        Assert.Empty(result.Errors);
        var bytes = Assert.IsType<byte[]>(result.Output);
        var esperado = new List<byte>(new byte[] { 0x1B, 0x61, 0x01 }); // ESC a 1 (centrado)
        for (int i = 0; i < lineas.Length; i++)
        {
            esperado.AddRange(Encoding.ASCII.GetBytes(lineas[i]));
            esperado.Add(0x0A);
        }
        Assert.True(IndexOf(bytes, esperado.ToArray()) >= 0,
            "Las líneas deberían salir centradas y separadas por LF, cada una completa.");
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i + needle.Length <= haystack.Length; i++)
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
                return i;
        return -1;
    }
}
