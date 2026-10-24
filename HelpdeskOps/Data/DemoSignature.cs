using System.IO.Compression;

namespace HelpdeskOps.Data;

// Draws a made-up handwritten-looking signature as a PNG, so demo zimmet forms have something to show.
// The curve shape depends on the name, so each demo person signs a little differently.
public static class DemoSignature
{
    private const int Width = 360;
    private const int Height = 110;

    public static byte[] Create(string name)
    {
        var seed = name.Aggregate(17, (hash, c) => hash * 31 + c);
        var random = new Random(seed);
        var pixels = new byte[Width * Height * 4];

        // A rising scribble of loops followed by an underline stroke
        var loops = 4 + random.Next(3);
        var amplitude = 22 + random.Next(10);
        for (var t = 0.0; t <= 1.0; t += 0.0004)
        {
            var x = 20 + t * 300;
            var y = 60 - t * 15 + Math.Sin(t * loops * 2 * Math.PI) * amplitude * (1 - t * 0.4)
                + Math.Cos(t * loops * 5 * Math.PI) * 6;
            Dot(pixels, x + Math.Sin(t * loops * 2 * Math.PI) * 10, y);
        }
        for (var t = 0.0; t <= 1.0; t += 0.002)
        {
            Dot(pixels, 40 + t * 280, 92 - t * 8 + Math.Sin(t * Math.PI) * 4);
        }

        return EncodePng(pixels);
    }

    private static void Dot(byte[] pixels, double cx, double cy)
    {
        for (var y = (int)cy - 1; y <= (int)cy + 1; y++)
        {
            for (var x = (int)cx - 1; x <= (int)cx + 1; x++)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                var i = (y * Width + x) * 4;
                pixels[i] = 0x1b;
                pixels[i + 1] = 0x1f;
                pixels[i + 2] = 0x3b;
                pixels[i + 3] = 255;
            }
        }
    }

    // Minimal RGBA PNG writer: one IDAT chunk, filter type 0 on every row
    private static byte[] EncodePng(byte[] pixels)
    {
        using var raw = new MemoryStream();
        using (var zlib = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (var y = 0; y < Height; y++)
            {
                zlib.WriteByte(0);
                zlib.Write(pixels, y * Width * 4, Width * 4);
            }
        }

        using var png = new MemoryStream();
        png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var header = new byte[13];
        WriteInt(header, 0, Width);
        WriteInt(header, 4, Height);
        header[8] = 8;  // bit depth
        header[9] = 6;  // RGBA
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", raw.ToArray());
        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var buffer = new byte[4];
        WriteInt(buffer, 0, data.Length);
        stream.Write(buffer);
        var typeAndData = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        stream.Write(typeAndData);
        WriteInt(buffer, 0, (int)Crc32(typeAndData));
        stream.Write(buffer);
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }
}
