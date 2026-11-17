using System.Buffers.Binary;
using System.IO.Compression;

namespace MotorDsl.Pax;

/// <summary>
/// Utilidades PNG sin dependencias de plataforma: firma, lectura del IHDR y recorte horizontal
/// (para que la impresora simulada vuelque exactamente el ancho del perfil). El recorte soporta
/// PNG no entrelazado de 8 bits por canal (tipos de color 0, 2, 3, 4 y 6), que es lo que emite
/// SkiaSharp en raster-preview; para otro formato mas ancho que el perfil se lanza
/// <see cref="NotSupportedException"/>.
/// </summary>
public static class PaxPng
{
    private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public readonly record struct Header(int Width, int Height, byte BitDepth, byte ColorType, byte Interlace);

    public static bool HasSignature(ReadOnlySpan<byte> data)
        => data.Length >= Signature.Length && data[..Signature.Length].SequenceEqual(Signature);

    public static Header ReadHeader(byte[] png)
    {
        if (!HasSignature(png) || png.Length < 33) throw new FormatException("No es un PNG");
        var chunks = ReadChunks(png);
        if (chunks.Count == 0 || chunks[0].Type != "IHDR" || chunks[0].Data.Length != 13)
            throw new FormatException("PNG sin IHDR");
        var d = chunks[0].Data;
        return new Header(
            BinaryPrimitives.ReadInt32BigEndian(d.AsSpan(0, 4)),
            BinaryPrimitives.ReadInt32BigEndian(d.AsSpan(4, 4)),
            d[8], d[9], d[12]);
    }

    /// <summary>Devuelve el PNG con a lo sumo <paramref name="maxWidth"/> columnas (recorte a la derecha).</summary>
    public static byte[] CropToWidth(byte[] png, int maxWidth)
    {
        var h = ReadHeader(png);
        if (h.Width <= maxWidth) return png;

        int channels = h.ColorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => -1 };
        if (h.BitDepth != 8 || h.Interlace != 0 || channels < 0)
            throw new NotSupportedException($"Recorte no soportado: bitDepth={h.BitDepth} colorType={h.ColorType} interlace={h.Interlace}");

        var chunks = ReadChunks(png);
        using var idat = new MemoryStream();
        foreach (var c in chunks.Where(c => c.Type == "IDAT")) idat.Write(c.Data);
        idat.Position = 0;
        using var raw = new MemoryStream();
        using (var z = new ZLibStream(idat, CompressionMode.Decompress)) z.CopyTo(raw);
        var data = raw.ToArray();

        int bpp = channels;
        int stride = h.Width * bpp;
        if (data.Length < (stride + 1) * h.Height) throw new FormatException("IDAT truncado");

        int newStride = maxWidth * bpp;
        var output = new byte[(newStride + 1) * h.Height];
        var prev = new byte[stride];
        var cur = new byte[stride];
        for (int y = 0; y < h.Height; y++)
        {
            int off = y * (stride + 1);
            byte filter = data[off];
            Array.Copy(data, off + 1, cur, 0, stride);
            Unfilter(filter, cur, prev, bpp);
            int o = y * (newStride + 1);
            output[o] = 0;
            Array.Copy(cur, 0, output, o + 1, newStride);
            (prev, cur) = (cur, prev);
        }

        using var comp = new MemoryStream();
        using (var z = new ZLibStream(comp, CompressionLevel.Optimal, leaveOpen: true)) z.Write(output);

        var ihdr = (byte[])chunks[0].Data.Clone();
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), maxWidth);

        using var outPng = new MemoryStream();
        outPng.Write(Signature);
        WriteChunk(outPng, "IHDR", ihdr);
        foreach (var c in chunks.Where(c => c.Type is "PLTE" or "tRNS")) WriteChunk(outPng, c.Type, c.Data);
        WriteChunk(outPng, "IDAT", comp.ToArray());
        WriteChunk(outPng, "IEND", Array.Empty<byte>());
        return outPng.ToArray();
    }

    private static void Unfilter(byte filter, byte[] cur, byte[] prev, int bpp)
    {
        for (int i = 0; i < cur.Length; i++)
        {
            int a = i >= bpp ? cur[i - bpp] : 0;
            int b = prev[i];
            int c = i >= bpp ? prev[i - bpp] : 0;
            int add = filter switch
            {
                0 => 0,
                1 => a,
                2 => b,
                3 => (a + b) >> 1,
                4 => Paeth(a, b, c),
                _ => throw new FormatException($"Filtro PNG invalido {filter}")
            };
            cur[i] = (byte)(cur[i] + add);
        }
    }

    internal static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return a;
        return pb <= pc ? b : c;
    }

    private readonly record struct Chunk(string Type, byte[] Data);

    private static List<Chunk> ReadChunks(byte[] png)
    {
        var list = new List<Chunk>();
        int pos = Signature.Length;
        while (pos + 12 <= png.Length)
        {
            int len = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(pos, 4));
            if (len < 0 || pos + 12 + len > png.Length) throw new FormatException("Chunk PNG truncado");
            var type = System.Text.Encoding.ASCII.GetString(png, pos + 4, 4);
            list.Add(new Chunk(type, png.AsSpan(pos + 8, len).ToArray()));
            pos += 12 + len;
            if (type == "IEND") break;
        }
        return list;
    }

    internal static void WriteChunk(Stream s, string type, byte[] data)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf, data.Length);
        s.Write(buf);
        var t = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(t);
        s.Write(data);
        uint crc = Crc32(t, 0xFFFFFFFFu);
        crc = Crc32(data, crc) ^ 0xFFFFFFFFu;
        BinaryPrimitives.WriteUInt32BigEndian(buf, crc);
        s.Write(buf);
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[n] = c;
        }
        return t;
    }

    private static uint Crc32(byte[] data, uint crc)
    {
        foreach (var b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }
}
