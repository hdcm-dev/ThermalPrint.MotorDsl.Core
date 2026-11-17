namespace MotorDsl.Pax;

/// <summary>
/// Abstraccion propia entre <see cref="PaxPrinterTransport"/> y el SDK NeptuneLite. Las
/// implementaciones son la real (NeptuneLite, unidad U2) y <see cref="SimulatedPaxPrinterDevice"/>.
/// <para>
/// Contrato de errores: los codigos del SDK NO se traducen aca. <see cref="GetStatusAsync"/> y
/// <see cref="PrintAsync"/> devuelven el codigo crudo de <c>getStatus()</c> / <c>onError</c>
/// (0 = ok), y las fallas que el SDK informa por excepcion se lanzan como
/// <see cref="PaxSdkException"/>. La traduccion vive en <see cref="PaxErrorMapping"/>.
/// </para>
/// </summary>
public interface IPaxPrinterDevice
{
    /// <summary>Obtiene la DAL y la impresora (carga diferida: recien aca se llama a getDal).</summary>
    Task OpenAsync(CancellationToken ct = default);

    /// <summary>Codigo crudo de <c>IPrinter.getStatus()</c>.</summary>
    Task<int> GetStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Imprime el PNG (vía bitmap (a): <c>print(Bitmap, IPinterListener)</c>) y devuelve el codigo
    /// del listener: 0 en <c>onSucc</c>, el de <c>onError</c> en otro caso.
    /// </summary>
    Task<int> PrintAsync(byte[] png, CancellationToken ct = default);

    Task CloseAsync();
}
