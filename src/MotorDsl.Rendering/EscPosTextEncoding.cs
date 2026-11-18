using MotorDsl.Core.Models;
using System.Text;

namespace MotorDsl.Rendering;

/// <summary>
/// Codificación del texto que viaja como caracteres a una impresora ESC/POS: la tabla que se
/// selecciona con <c>ESC t n</c> y la página de códigos con la que se codifica el texto, que
/// tienen que ser la misma. La comparten <see cref="EscPosRenderer"/> (<c>escpos</c>) y el
/// <c>BitmapEscPosRenderer</c> de MotorDsl.Maui (<c>escpos-bitmap</c>).
/// <para>
/// <b>Por defecto, PC437</b> (<c>ESC t 0</c>): es la tabla que declara el self-test de la
/// 58HB6-6101 (<c>docs/10_developer_guide/perfiles-impresoras-reales.md</c>) y la que
/// <c>EscPosRenderer</c> usó siempre. Imprime á é í ó ú ñ Ñ ü Ü É ° º ¿ ¡, pero no tiene Á Í Ó Ú:
/// la codificación de .NET las degrada a la letra sin acento (Á → A), no a «?».
/// </para>
/// <para>
/// <b>Otra tabla se elige en el perfil</b>, con <see cref="DeviceProfile.CodePage"/> y
/// <see cref="DeviceProfile.CodePageCommand"/>. Para cualquier página distinta de 437 el comando
/// es obligatorio: el número <c>n</c> de <c>ESC t</c> lo fija cada fabricante (en la referencia de
/// Epson, PC850 es 2 y PC858 es 19, pero los clones no siempre lo respetan), así que se toma del
/// self-test del equipo y no se supone. Por ejemplo, para PC858 en una impresora que lo numere
/// como Epson: <c>CodePage = 858, CodePageCommand = { 0x1B, 0x74, 0x13 }</c>.
/// </para>
/// </summary>
public sealed class EscPosTextEncoding
{
    /// <summary>Página por defecto cuando el perfil no declara <see cref="DeviceProfile.CodePage"/>.</summary>
    public const int DefaultCodePage = 437;

    /// <summary><c>ESC t 0</c>: selecciona PC437, la tabla del self-test de la 58HB6-6101.</summary>
    public static readonly byte[] SelectPc437 = { 0x1B, 0x74, 0x00 };

    private readonly Encoding _encoding;

    /// <summary>Página de códigos con la que se codifica el texto.</summary>
    public int CodePage { get; }

    /// <summary>Comando que selecciona en la impresora la tabla de <see cref="CodePage"/>.</summary>
    public byte[] SelectCommand { get; }

    private EscPosTextEncoding(int codePage, byte[] selectCommand, Encoding encoding)
    {
        CodePage = codePage;
        SelectCommand = selectCommand;
        _encoding = encoding;
    }

    /// <summary>
    /// Resuelve la codificación del perfil: <see cref="DeviceProfile.CodePage"/> (437 si es null) y
    /// <see cref="DeviceProfile.CodePageCommand"/> (<c>ESC t 0</c> si es null y la página es 437).
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// La página no es 437 y el perfil no trae su <c>CodePageCommand</c>, o .NET no la conoce.
    /// </exception>
    public static EscPosTextEncoding For(DeviceProfile? profile)
    {
        var codePage = profile?.CodePage ?? DefaultCodePage;
        var command = profile?.CodePageCommand;

        if (command == null)
        {
            if (codePage != DefaultCodePage)
                throw new NotSupportedException(
                    $"El perfil '{profile?.Name}' pide la página de códigos {codePage} sin CodePageCommand: " +
                    "el número de ESC t depende del fabricante; declaralo según el self-test de la impresora.");
            command = SelectPc437;
        }

        var encoding = CodePagesEncodingProvider.Instance.GetEncoding(codePage)
            ?? throw new NotSupportedException($"Página de códigos {codePage} no disponible en .NET.");

        return new EscPosTextEncoding(codePage, command, encoding);
    }

    /// <summary>
    /// Codifica el texto con la página de <see cref="CodePage"/>. Un texto ASCII sale igual que con
    /// <see cref="Encoding.ASCII"/> en cualquier página ESC/POS; lo que la página no tiene se degrada
    /// a su aproximación (Á → A en 437) y, si no hay, a «?».
    /// </summary>
    public byte[] Encode(string? text) => _encoding.GetBytes(text ?? "");

    /// <summary>True si el texto es ASCII de 7 bits: se imprime igual con cualquier tabla.</summary>
    public static bool IsAscii(string? text)
    {
        if (string.IsNullOrEmpty(text)) return true;
        foreach (var c in text)
            if (c > 0x7F) return false;
        return true;
    }
}
