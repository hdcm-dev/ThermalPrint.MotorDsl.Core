namespace MotorDsl.Pax;

/// <summary>
/// Falla de conexion con la impresora interna (sin NeptuneService, DAL que no carga, 99/101/103,
/// librerias nativas ausentes). Hereda de <see cref="IOException"/> para que
/// <c>PrintError.FromException</c> la clasifique como <c>Connection</c> sin tocar el Core.
/// </summary>
public sealed class PaxConnectionException : IOException
{
    public PaxConnectionException(string message, int? code = null, Exception? inner = null)
        : base(message, inner)
        => Code = code;

    /// <summary>Codigo del SDK si lo hubo (99, 101, 103); null para LOAD DAL ERR o falla nativa.</summary>
    public int? Code { get; }
}
