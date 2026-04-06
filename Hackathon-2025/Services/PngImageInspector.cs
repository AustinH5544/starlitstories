using System.Buffers.Binary;
using System.IO.Compression;

namespace Hackathon_2025.Services;

internal sealed record PngImageStats(
    int Width,
    int Height,
    double AverageBrightness,
    double BrightnessStdDev,
    double NearBlackRatio,
    bool IsSuspiciouslyDark);

internal static class PngImageInspector
{
    private static ReadOnlySpan<byte> PngSignature => new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };

    public static bool TryAnalyze(byte[] pngBytes, out PngImageStats? stats)
    {
        stats = null;

        try
        {
            if (!HasPngSignature(pngBytes))
                return false;

            var reader = new ChunkReader(pngBytes);
            if (!reader.TryReadHeader(out var width, out var height, out var bitDepth, out var colorType))
                return false;

            if (bitDepth != 8)
                return false;

            if (!TryGetBytesPerPixel(colorType, out var bytesPerPixel))
                return false;

            if (!reader.TryReadIdatData(out var compressed))
                return false;

            var stride = checked(width * bytesPerPixel);
            var expectedRawLength = checked(height * (stride + 1));
            var raw = Decompress(compressed, expectedRawLength);
            if (raw.Length < expectedRawLength)
                return false;

            var recon = ReconstructScanlines(raw, width, height, bytesPerPixel, stride);
            stats = ComputeStats(recon, width, height, colorType, bytesPerPixel);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasPngSignature(byte[] bytes) =>
        bytes.Length >= PngSignature.Length &&
        bytes.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature);

    private static bool TryGetBytesPerPixel(byte colorType, out int bytesPerPixel)
    {
        bytesPerPixel = colorType switch
        {
            0 => 1, // grayscale
            2 => 3, // RGB
            4 => 2, // grayscale + alpha
            6 => 4, // RGBA
            _ => 0
        };

        return bytesPerPixel > 0;
    }

    private static byte[] Decompress(byte[] compressed, int expectedRawLength)
    {
        using var input = new MemoryStream(compressed);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream(expectedRawLength);
        zlib.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] ReconstructScanlines(byte[] raw, int width, int height, int bytesPerPixel, int stride)
    {
        var recon = new byte[height * stride];
        var srcOffset = 0;

        for (var y = 0; y < height; y++)
        {
            var filterType = raw[srcOffset++];
            var rowStart = y * stride;
            var prevRowStart = rowStart - stride;

            for (var x = 0; x < stride; x++)
            {
                var filtered = raw[srcOffset++];
                var left = x >= bytesPerPixel ? recon[rowStart + x - bytesPerPixel] : (byte)0;
                var up = y > 0 ? recon[prevRowStart + x] : (byte)0;
                var upLeft = y > 0 && x >= bytesPerPixel ? recon[prevRowStart + x - bytesPerPixel] : (byte)0;

                recon[rowStart + x] = filterType switch
                {
                    0 => filtered,
                    1 => (byte)(filtered + left),
                    2 => (byte)(filtered + up),
                    3 => (byte)(filtered + ((left + up) >> 1)),
                    4 => (byte)(filtered + PaethPredictor(left, up, upLeft)),
                    _ => throw new InvalidDataException($"Unsupported PNG filter type {filterType}.")
                };
            }
        }

        return recon;
    }

    private static PngImageStats ComputeStats(byte[] pixelBytes, int width, int height, byte colorType, int bytesPerPixel)
    {
        var pixelCount = width * height;
        double sum = 0;
        double sumSq = 0;
        var nearBlackCount = 0;

        for (var i = 0; i < pixelCount; i++)
        {
            var offset = i * bytesPerPixel;
            var brightness = colorType switch
            {
                0 => pixelBytes[offset],
                2 => Luma(pixelBytes[offset], pixelBytes[offset + 1], pixelBytes[offset + 2]),
                4 => ApplyAlpha(pixelBytes[offset], pixelBytes[offset + 1]),
                6 => ApplyAlpha(
                    Luma(pixelBytes[offset], pixelBytes[offset + 1], pixelBytes[offset + 2]),
                    pixelBytes[offset + 3]),
                _ => throw new InvalidDataException($"Unsupported PNG color type {colorType}.")
            };

            sum += brightness;
            sumSq += brightness * brightness;
            if (brightness <= 8)
                nearBlackCount++;
        }

        var average = sum / pixelCount;
        var variance = Math.Max(0, (sumSq / pixelCount) - (average * average));
        var stdDev = Math.Sqrt(variance);
        var nearBlackRatio = nearBlackCount / (double)pixelCount;
        var suspicious = average < 12 && stdDev < 12 && nearBlackRatio > 0.92;

        return new PngImageStats(width, height, average, stdDev, nearBlackRatio, suspicious);
    }

    private static byte Luma(byte r, byte g, byte b) =>
        (byte)Math.Clamp((0.2126 * r) + (0.7152 * g) + (0.0722 * b), 0, 255);

    private static byte ApplyAlpha(byte brightness, byte alpha) =>
        (byte)Math.Clamp((brightness * alpha) / 255.0, 0, 255);

    private static int PaethPredictor(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);

        if (pa <= pb && pa <= pc) return a;
        if (pb <= pc) return b;
        return c;
    }

    private ref struct ChunkReader
    {
        private readonly ReadOnlySpan<byte> _bytes;
        private int _offset;

        public ChunkReader(byte[] bytes)
        {
            _bytes = bytes;
            _offset = PngSignature.Length;
        }

        public bool TryReadHeader(out int width, out int height, out byte bitDepth, out byte colorType)
        {
            width = 0;
            height = 0;
            bitDepth = 0;
            colorType = 0;

            if (!TryReadChunk(out var type, out var data) || type != "IHDR" || data.Length < 13)
                return false;

            width = BinaryPrimitives.ReadInt32BigEndian(data[..4]);
            height = BinaryPrimitives.ReadInt32BigEndian(data.Slice(4, 4));
            bitDepth = data[8];
            colorType = data[9];
            return width > 0 && height > 0;
        }

        public bool TryReadIdatData(out byte[] compressed)
        {
            compressed = Array.Empty<byte>();
            using var output = new MemoryStream();

            while (TryReadChunk(out var type, out var data))
            {
                if (type == "IDAT")
                {
                    output.Write(data);
                    continue;
                }

                if (type == "IEND")
                {
                    compressed = output.ToArray();
                    return compressed.Length > 0;
                }
            }

            return false;
        }

        private bool TryReadChunk(out string type, out ReadOnlySpan<byte> data)
        {
            type = string.Empty;
            data = ReadOnlySpan<byte>.Empty;

            if (_offset + 8 > _bytes.Length)
                return false;

            var length = BinaryPrimitives.ReadInt32BigEndian(_bytes.Slice(_offset, 4));
            _offset += 4;

            if (length < 0 || _offset + 4 + length + 4 > _bytes.Length)
                return false;

            type = System.Text.Encoding.ASCII.GetString(_bytes.Slice(_offset, 4));
            _offset += 4;
            data = _bytes.Slice(_offset, length);
            _offset += length + 4; // skip data + CRC
            return true;
        }
    }
}
