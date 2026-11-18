using MotorDsl.Core.Layout;
using MotorDsl.Core.Models;
using MotorDsl.Maui.Renderers;
using MotorDsl.Rendering;

namespace MotorDsl.Tests;

/// <summary>
/// Texto con acentos en ESC/POS. <c>escpos-bitmap</c> (el renderer de la impresora Bluetooth
/// 58HB6) codificaba el texto en ASCII: é, ó, ñ, °, ¿, ¡ salían como «?». Ahora usa la misma
/// tabla que <c>escpos</c> (PC437 por defecto, <c>ESC t 0</c>), resuelta en
/// <see cref="EscPosTextEncoding"/> desde el perfil.
/// </summary>
public class EscPosTextEncodingTests
{
    private static readonly byte[] Init = { 0x1B, 0x40 };
    private static readonly byte[] EscT0 = { 0x1B, 0x74, 0x00 };
    private static readonly byte[] AlignLeft = { 0x1B, 0x61, 0x00 };
    private static readonly byte[] CutPartial = { 0x1D, 0x56, 0x01 };

    // Bytes PC437 esperados, de la tabla de docs/10_developer_guide/perfiles-impresoras-reales.md
    // (ñ A4, Ñ A5, ó A2, ú A3) y de la tabla IBM 437 (° F8).
    private static readonly byte[] ActaNro437 = { 0x41, 0x63, 0x74, 0x61, 0x20, 0x4E, 0xF8 };                    // "Acta N°"
    private static readonly byte[] Infraccion437 = { 0x49, 0x6E, 0x66, 0x72, 0x61, 0x63, 0x63, 0x69, 0xA2, 0x6E }; // "Infracción"
    private static readonly byte[] Nandu437 = { 0xA5, 0x61, 0x6E, 0x64, 0xA3 };                                 // "Ñandú"

    private static DeviceProfile Perfil58() => new("bt_58mm", 32, "escpos-bitmap");

    private static LayoutedDocument Layout(DeviceProfile profile, params string[] textos)
    {
        var root = new ContainerNode("vertical", textos.Select(t => (DocumentNode)new TextNode(t)).ToList());
        var evaluated = new EvaluatedDocument { Id = "acentos", Version = "1.0", Root = root };
        return new LayoutEngine().ApplyLayout(evaluated, profile);
    }

    private static byte[] RenderBitmap(DeviceProfile profile, params string[] textos)
    {
        var result = new BitmapEscPosRenderer(new SkiaSharpRasterizer()).Render(Layout(profile, textos), profile);
        Assert.Empty(result.Errors);
        return Assert.IsType<byte[]>(result.Output);
    }

    private static byte[] RenderEscPos(DeviceProfile profile, params string[] textos)
    {
        var result = new EscPosRenderer().Render(Layout(profile, textos), profile);
        Assert.Empty(result.Errors);
        return Assert.IsType<byte[]>(result.Output);
    }

    private static byte[] Concat(params byte[][] partes) => partes.SelectMany(p => p).ToArray();

    // ─── escpos-bitmap ───

    [Theory]
    [InlineData("Acta N°")]
    [InlineData("Infracción")]
    [InlineData("Ñandú")]
    public void EscPosBitmap_TextoConAcentos_SeleccionaPc437YCodificaEnPc437(string texto)
    {
        var esperado = texto switch
        {
            "Acta N°" => ActaNro437,
            "Infracción" => Infraccion437,
            _ => Nandu437,
        };

        var bytes = RenderBitmap(Perfil58(), texto);

        Assert.Equal(Concat(Init, EscT0, AlignLeft, esperado, new byte[] { 0x0A }, CutPartial), bytes);
    }

    [Fact]
    public void EscPosBitmap_TextoAsciiPuro_SaleByteAByteComoAntes()
    {
        // Sin ESC t: un ticket ASCII no cambia respecto de la versión que codificaba en ASCII.
        var bytes = RenderBitmap(Perfil58(), "Acta Nro 123", "Total: $ 4500");

        Assert.Equal(Concat(
            Init,
            AlignLeft, "Acta Nro 123"u8.ToArray(), new byte[] { 0x0A },
            AlignLeft, "Total: $ 4500"u8.ToArray(), new byte[] { 0x0A },
            CutPartial), bytes);
    }

    [Fact]
    public void EscPosBitmap_SignosDeApertura_NoSalenComoSignoDePregunta()
    {
        var bytes = RenderBitmap(Perfil58(), "¿Firma? ¡Sí!");

        // ¿ A8, ¡ AD, í A1; el único 0x3F es el «?» que está en el texto.
        Assert.Equal(new byte[] { 0xA8, 0x46, 0x69, 0x72, 0x6D, 0x61, 0x3F, 0x20, 0xAD, 0x53, 0xA1, 0x21 },
            bytes.Skip(Init.Length + EscT0.Length + AlignLeft.Length).Take(12).ToArray());
    }

    [Fact]
    public void EscPosBitmap_MayusculaAcentuadaQuePc437NoTiene_SeDegradaALaLetraSinAcento()
    {
        // PC437 no tiene Á Í Ó Ú: mejor «A» que «?». É sí está (0x90).
        var bytes = RenderBitmap(Perfil58(), "ÁÉÍÓÚ");

        Assert.Equal(new byte[] { 0x41, 0x90, 0x49, 0x4F, 0x55 },
            bytes.Skip(Init.Length + EscT0.Length + AlignLeft.Length).Take(5).ToArray());
    }

    [Fact]
    public void EscPosBitmap_PerfilConOtraTabla_UsaSuComandoYSuPagina()
    {
        // PC858 con la numeración de Epson (ESC t 19). El número lo pone el perfil, no la librería.
        var profile = Perfil58();
        profile.CodePage = 858;
        profile.CodePageCommand = new byte[] { 0x1B, 0x74, 0x13 };

        var bytes = RenderBitmap(profile, "ÁÍÓÚ €");

        Assert.Equal(Concat(Init, new byte[] { 0x1B, 0x74, 0x13 }, AlignLeft,
            new byte[] { 0xB5, 0xD6, 0xE0, 0xE9, 0x20, 0xD5, 0x0A }, CutPartial), bytes);
    }

    [Fact]
    public void EscPosBitmap_PaginaDistintaDe437SinComando_DevuelveErrorYNoImprimeBasura()
    {
        var profile = Perfil58();
        profile.CodePage = 850;

        var result = new BitmapEscPosRenderer(new SkiaSharpRasterizer()).Render(Layout(profile, "Ñandú"), profile);

        Assert.Contains(result.Errors, e => e.Contains("850") && e.Contains("CodePageCommand"));
        Assert.Empty(Assert.IsType<byte[]>(result.Output));
    }

    // ─── escpos: misma codificación, salida por defecto sin cambios ───

    [Fact]
    public void EscPos_PorDefecto_SigueEmitiendoEscT0YPc437()
    {
        var profile = new DeviceProfile("bt_58mm", 32, "escpos");

        var bytes = RenderEscPos(profile, "Acta N°", "Infracción", "Ñandú");

        Assert.Equal(Concat(
            Init, EscT0,
            AlignLeft, ActaNro437, new byte[] { 0x0A },
            AlignLeft, Infraccion437, new byte[] { 0x0A },
            AlignLeft, Nandu437, new byte[] { 0x0A },
            CutPartial), bytes);
    }

    [Fact]
    public void EscPos_PerfilConOtraTabla_UsaSuComandoYSuPagina()
    {
        var profile = new DeviceProfile("bt_58mm", 32, "escpos")
        {
            CodePage = 850,
            CodePageCommand = new byte[] { 0x1B, 0x74, 0x02 },
        };

        var bytes = RenderEscPos(profile, "Ó");

        Assert.Equal(Concat(Init, new byte[] { 0x1B, 0x74, 0x02 }, AlignLeft, new byte[] { 0xE0, 0x0A }, CutPartial), bytes);
    }

    [Fact]
    public void EscPos_YEscPosBitmap_CodificanIgualElTexto()
    {
        var texto = "Acta N° 1 – Infracción ¿Ñandú?";

        var escpos = RenderEscPos(new DeviceProfile("p", 48, "escpos"), texto);
        var bitmap = RenderBitmap(new DeviceProfile("p", 48, "escpos-bitmap"), texto);

        Assert.Equal(escpos, bitmap);
    }

    // ─── EscPosTextEncoding ───

    [Fact]
    public void For_PerfilNulo_EsPc437ConEscT0()
    {
        var enc = EscPosTextEncoding.For(null);

        Assert.Equal(437, enc.CodePage);
        Assert.Equal(EscT0, enc.SelectCommand);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("Acta 123 ~", true)]
    [InlineData("Acta N°", false)]
    [InlineData("ñ", false)]
    public void IsAscii(string texto, bool esperado) => Assert.Equal(esperado, EscPosTextEncoding.IsAscii(texto));
}
