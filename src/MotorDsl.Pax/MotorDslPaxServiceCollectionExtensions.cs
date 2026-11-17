using Microsoft.Extensions.DependencyInjection;
using MotorDsl.Printing;

namespace MotorDsl.Pax;

public static class MotorDslPaxServiceCollectionExtensions
{
    /// <summary>
    /// Registra el transport PAX (Kind "pax") con AddSingleton, igual que Bluetooth y red: el
    /// servicio rutea por Kind y los transports coexisten. Con <see cref="PaxPrinterMode.Simulated"/>
    /// usa <see cref="SimulatedPaxPrinterDevice"/> (solo builds de desarrollo); con Real, la
    /// <see cref="PaxPrinterOptions.RealDeviceFactory"/> (sin ella, conectar falla con Connection).
    /// </summary>
    public static IServiceCollection AddPaxPrinterTransport(
        this IServiceCollection services,
        Action<PaxPrinterOptions>? configure = null)
    {
        var options = new PaxPrinterOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IPaxPrinterDevice>(_ => CreateDevice(options));
        services.AddSingleton<IThermalPrinterTransport>(sp => new PaxPrinterTransport(sp.GetRequiredService<IPaxPrinterDevice>()));
        return services;
    }

    internal static IPaxPrinterDevice CreateDevice(PaxPrinterOptions options) => options.Mode switch
    {
        PaxPrinterMode.Simulated => new SimulatedPaxPrinterDevice(options),
        _ => options.RealDeviceFactory?.Invoke(options) ?? new UnavailablePaxPrinterDevice()
    };
}
