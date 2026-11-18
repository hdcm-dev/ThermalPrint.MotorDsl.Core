namespace MotorDsl.Pax;

/// <summary>
/// Dispositivo usado en modo Real cuando no hay implementacion real registrada
/// (<see cref="PaxPrinterOptions.RealDeviceFactory"/> nula o que devuelve null). Falla al abrir
/// como si faltara la DAL, de modo que el resultado es Connection y la app no se cae.
/// </summary>
internal sealed class UnavailablePaxPrinterDevice : IPaxPrinterDevice
{
    private static PaxSdkException Missing()
        => new(PaxErrorSource.NativeLoad, 0, "no hay implementacion real de IPaxPrinterDevice registrada (PaxPrinterOptions.RealDeviceFactory)");

    public Task OpenAsync(CancellationToken ct = default) => throw Missing();
    public Task<int> GetStatusAsync(CancellationToken ct = default) => throw Missing();
    public Task<int> PrintAsync(byte[] png, CancellationToken ct = default) => throw Missing();
    public Task CloseAsync() => Task.CompletedTask;
}
