namespace MotorDsl.Pax;

/// <summary>
/// Dispositivo usado en modo Real cuando no hay fabrica del SDK (<see cref="PaxPrinterOptions.RealDeviceFactory"/>
/// nula, p. ej. antes del binding NeptuneLite de U2). Falla al abrir como si faltara la DAL, de
/// modo que el resultado es Connection y la app no se cae.
/// </summary>
internal sealed class UnavailablePaxPrinterDevice : IPaxPrinterDevice
{
    private static PaxSdkException Missing()
        => new(PaxErrorSource.NativeLoad, 0, "SDK NeptuneLite no disponible en este paquete");

    public Task OpenAsync(CancellationToken ct = default) => throw Missing();
    public Task<int> GetStatusAsync(CancellationToken ct = default) => throw Missing();
    public Task<int> PrintAsync(byte[] png, CancellationToken ct = default) => throw Missing();
    public Task CloseAsync() => Task.CompletedTask;
}
