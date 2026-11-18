namespace MotorDsl.Pax;

/// <summary>Origen del codigo de error del SDK; las numeraciones de cada tabla no coinciden.</summary>
public enum PaxErrorSource
{
    /// <summary>Codigo de <c>getStatus()</c>, <c>start()</c> u <c>onError</c> del listener.</summary>
    Status,
    /// <summary>Codigo de <c>EPrintDevException</c>.</summary>
    PrintDevException,
    /// <summary><c>Exception("LOAD DAL ERR")</c> de NeptuneLiteUser (falta NeptuneService o la DAL).</summary>
    LoadDal,
    /// <summary><c>UnsatisfiedLinkError</c> u otra falla nativa al cargar el SDK (p. ej. en x86).</summary>
    NativeLoad
}

/// <summary>
/// Falla del SDK tal como la informa la implementacion de <see cref="IPaxPrinterDevice"/>, sin
/// clasificar. <see cref="PaxErrorMapping.ToException"/> la convierte en la excepcion que entiende
/// <c>PrintError.FromException</c>.
/// </summary>
public sealed class PaxSdkException : Exception
{
    public PaxSdkException(PaxErrorSource source, int code, string? message = null, Exception? inner = null)
        : base(message ?? $"PAX {source} {code}", inner)
    {
        Source = source;
        Code = code;
    }

    public new PaxErrorSource Source { get; }
    public int Code { get; }
}
