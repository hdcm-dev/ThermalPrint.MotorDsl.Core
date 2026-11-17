#if MOTORDSL_PAX_NEPTUNE
using Android.Content;
using Android.Graphics;
using Com.Pax.Dal;
using Com.Pax.Dal.Exceptions;
using Com.Pax.Neptunelite.Api;
using MotorDsl.Core.Models;

namespace MotorDsl.Pax.Neptune;

/// <summary>
/// Impresora interna real del PAX A910S via NeptuneLite 4.15.00 (vía bitmap (a)).
/// <list type="bullet">
/// <item>Carga diferida: <c>getDal</c> recien en <see cref="OpenAsync"/>; nunca en el constructor ni al registrar.</item>
/// <item>Toda falla Java (<see cref="Java.Lang.Throwable"/>) se traduce a <see cref="PaxSdkException"/>; el
/// transporte la mapea con <see cref="PaxErrorMapping"/>. LOAD DAL ERR, InvocationTargetException,
/// ExceptionInInitializerError, NoClassDefFoundError o UnsatisfiedLinkError (x86, sin NeptuneService)
/// terminan en Connection y la app no se cae.</item>
/// <item><see cref="GetStatusAsync"/> es <c>getStatus()</c>; <see cref="PrintAsync"/> es
/// <c>print(Bitmap, IPinterListener)</c>, sin mezclar con init/printBitmap/start.</item>
/// <item>Todo corre fuera del hilo de UI (<see cref="Task.Run(Action)"/>).</item>
/// </list>
/// </summary>
public sealed class NeptunePaxPrinterDevice : IPaxPrinterDevice
{
    private readonly Context _context;
    private readonly object _gate = new();
    private IPrinter? _printer;

    public NeptunePaxPrinterDevice(Context? context = null)
    {
        _context = context ?? Android.App.Application.Context;
    }

    public Task OpenAsync(CancellationToken ct = default) => Task.Run(() =>
    {
        lock (_gate)
        {
            if (_printer is not null) return;
            try
            {
                var user = NeptuneLiteUser.Instance
                    ?? throw new PaxSdkException(PaxErrorSource.LoadDal, 0, "NeptuneLiteUser.getInstance() devolvio null");
                var dal = user.GetDal(_context)
                    ?? throw new PaxSdkException(PaxErrorSource.LoadDal, 0, "getDal devolvio null");
                _printer = dal.Printer
                    ?? throw new PaxSdkException(PaxErrorSource.PrintDevException, PaxErrorMapping.PrintDevConnect, "IDAL.getPrinter() devolvio null");
            }
            catch (PaxSdkException) { throw; }
            catch (Java.Lang.Throwable t) { throw Translate(t, opening: true); }
        }
    }, ct);

    public Task<int> GetStatusAsync(CancellationToken ct = default) => Task.Run(() =>
    {
        var printer = RequirePrinter();
        try { return printer.Status; }
        catch (Java.Lang.Throwable t) { throw Translate(t, opening: false); }
    }, ct);

    public async Task<int> PrintAsync(byte[] png, CancellationToken ct = default)
    {
        var printer = RequirePrinter();
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Bitmap? bitmap = null;

        await Task.Run(() =>
        {
            bitmap = BitmapFactory.DecodeByteArray(png, 0, png.Length)
                ?? throw new PrinterProtocolException("El PNG no se pudo decodificar a Android.Graphics.Bitmap");
            try { printer.Print(bitmap, new Listener(tcs)); }
            catch (Java.Lang.Throwable t) { throw Translate(t, opening: false); }
        }, ct).ConfigureAwait(false);

        try
        {
            // El timeout lo pone el transporte (WriteTimeoutMs) via ct.
            using (ct.Register(() => tcs.TrySetCanceled(ct)))
                return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            // Si se cancelo, el SDK puede seguir usando el bitmap: no se recicla en ese caso.
            if (tcs.Task.IsCompletedSuccessfully) bitmap?.Recycle();
        }
    }

    public Task CloseAsync()
    {
        lock (_gate) { _printer = null; }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Diagnostico (Inferido, R3 §5): si el paquete de NeptuneService esta instalado. El nombre
    /// <c>com.pax.ipp.neptune</c> sale del manifiesto de la APK del material y se confirma en el
    /// equipo (D1). En Android 11+ la APP declara &lt;queries&gt; con ese paquete (el .aar de esta libreria no lleva manifiesto; U6).
    /// </summary>
    public static bool IsNeptuneServiceInstalled(Context? context = null)
    {
        try
        {
            var ctx = context ?? Android.App.Application.Context;
            ctx.PackageManager?.GetPackageInfo(NeptuneServicePackage, 0);
            return true;
        }
        catch (Java.Lang.Throwable) { return false; }
    }

    public const string NeptuneServicePackage = "com.pax.ipp.neptune";

    private IPrinter RequirePrinter()
    {
        lock (_gate)
        {
            return _printer ?? throw new PaxSdkException(PaxErrorSource.PrintDevException,
                PaxErrorMapping.PrintDevConnect, "Impresora PAX no abierta (OpenAsync)");
        }
    }

    private PaxSdkException Translate(Java.Lang.Throwable t, bool opening)
    {
        var inner = new Exception(t.ToString());
        if (t is AGeneralException g)
            return new PaxSdkException(PaxErrorSource.PrintDevException, g.ErrCode, g.Message, inner);
        if (t is Java.Lang.Error)   // UnsatisfiedLinkError, NoClassDefFoundError, ExceptionInInitializerError
            return new PaxSdkException(PaxErrorSource.NativeLoad, 0, t.Message, inner);
        if (opening)                 // LOAD DAL ERR, InvocationTargetException y demas de getDal
        {
            var detalle = IsNeptuneServiceInstalled(_context) ? "NeptuneService instalado" : "NeptuneService no detectado";
            return new PaxSdkException(PaxErrorSource.LoadDal, 0, $"{t.Message} ({detalle})", inner);
        }
        return new PaxSdkException(PaxErrorSource.PrintDevException, PaxErrorMapping.PrintDevConnect, t.Message, inner);
    }

    private sealed class Listener : Java.Lang.Object, IPrinter.IPinterListener
    {
        private readonly TaskCompletionSource<int> _tcs;
        public Listener(TaskCompletionSource<int> tcs) => _tcs = tcs;
        public void OnSucc() => _tcs.TrySetResult(PaxErrorMapping.StatusOk);
        public void OnError(int errCode) => _tcs.TrySetResult(errCode);
    }
}
#endif
