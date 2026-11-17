using MotorDsl.Core.Models;

namespace MotorDsl.Pax;

/// <summary>
/// Perfiles de render del PAX A910S. La libreria no trae presets de DeviceProfile (se cargan con
/// AddProfiles); esta ayuda arma el del A910S para el target <c>raster-preview</c>, cuyo PNG es
/// lo que recibe <see cref="PaxPrinterTransport"/>.
/// </summary>
public static class PaxProfiles
{
    /// <summary>Ancho PROVISIONAL (Inferido) hasta medirlo en el equipo.</summary>
    public const int DefaultWidthPx = 384;

    public const string A910SName = "pax_a910s";

    public static DeviceProfile A910S(int widthPx = DefaultWidthPx)
    {
        if (widthPx <= 0) throw new ArgumentOutOfRangeException(nameof(widthPx));
        var profile = new DeviceProfile(A910SName, 32, "raster-preview");
        profile.SetCapability("supports_bitmap", true);
        profile.SetCapability("bitmap_max_width_px", widthPx);
        return profile;
    }
}
