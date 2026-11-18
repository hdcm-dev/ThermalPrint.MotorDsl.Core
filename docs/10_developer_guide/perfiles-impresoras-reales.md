# Perfiles de impresoras reales

Este documento recopila perfiles de impresoras térmicas reales probadas, con sus parámetros, comandos y mapeo de caracteres según self-test.

## 58HB6-6101 (Bluetooth)

- **Modelo:** 58HB6-6101
- **Ancho papel:** 58mm
- **Chars por línea:** 32
- **Codepage:** PC437 (IBM USA Std. Europe)
- **Comando ESC t:** 0x1B 0x74 0x00
- **Baudrate BT:** 115200
- **PIN:** 0000
- **Barcodes soportados:** UPC-A, UPC-E, EAN13, EAN8, CODE39, CODEBAR, ITF, CODE93, CODE128, QR

### Ejemplo de perfil en C#

```csharp
var profile58HB6 = new DeviceProfile("58HB6", 32, "escpos")
{
    CodePage = 437,
    CodePageCommand = new byte[] { 0x1B, 0x74, 0x00 },
    BaudRate = 115200,
    SupportedBarcodes = new List<string> 
    { 
        "UPC-A", "UPC-E", "EAN13", "EAN8", 
        "CODE39", "CODEBAR", "ITF", "CODE93", "CODE128", "QR" 
    }
};
profile58HB6.SetCapability("supports_qrcode", true);
profile58HB6.SetCapability("supports_barcode", true);
profile58HB6.SetCapability("supports_images", false);

// Capacidades de imagen bitmap (1bpp, GS v 0)
profile58HB6.SetCapability("supports_bitmap", true);
profile58HB6.SetCapability("bitmap_max_width_px", 384);
profile58HB6.SetCapability("bitmap_recommended_width_px", 320);
profile58HB6.SetCapability("bitmap_max_height_px", 800);
profile58HB6.SetCapability("bitmap_color_depth_bpp", 1);
profile58HB6.SetCapability("bitmap_binarization_threshold", 128);
profile58HB6.SetCapability("bitmap_command", "GS_v_0");
```

### Tabla de caracteres españoles en PC437

| Carácter | Hex  |
|----------|------|
| á        | 0xA0 |
| é        | 0x82 |
| í        | 0xA1 |
| ó        | 0xA2 |
| ú        | 0xA3 |
| ñ        | 0xA4 |
| Ñ        | 0xA5 |
| É        | 0x90 |
| °        | 0xF8 |
| ¿        | 0xA8 |
| ¡        | 0xAD |

PC437 **no tiene Á, Í, Ó, Ú**: la codificación las degrada a la letra sin acento (Á → A), no a «?».

### Tabla de caracteres en los renderers

`escpos` y `escpos-bitmap` codifican el texto con la misma tabla, resuelta en
`EscPosTextEncoding` a partir del `DeviceProfile`:

- Sin `CodePage`: PC437 y `ESC t 0`, la tabla del self-test de este modelo.
- Con `CodePage` distinto de 437, el perfil tiene que traer su `CodePageCommand` (`ESC t n`): el
  número `n` lo fija cada fabricante, así que se toma del self-test del equipo. Sin comando, el
  render devuelve un error en lugar de imprimir caracteres de otra tabla.
- `escpos` emite `ESC t` siempre tras el `ESC @`; `escpos-bitmap` sólo si algún texto lleva
  caracteres fuera de ASCII, de modo que un ticket ASCII sale igual que antes.

El self-test de la 58HB6-6101 sólo documenta PC437; que acepte PC850 o PC858 (que sí tienen
Á Í Ó Ú) no está medido.

### Self-test (fragmento)

```
58HB6-6101
Baud:115200
Print depth:38 level
Quality level:4 level
Temp:35C
BT Name:58HB6-6101
PIN:0000
BT Multi-connection:Not support
Barcodes:UPC-A,UPC-E,EAN13,EAN8,CODE39,CODEBAR,ITF,CODE93,CODE128,QR
```

### Notas
- El codepage PC437 permite imprimir correctamente caracteres españoles.
- El comando ESC t debe enviarse tras el Init para asegurar el codepage correcto.
- El perfil puede usarse como plantilla para otras impresoras similares.
