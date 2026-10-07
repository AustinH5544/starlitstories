using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Hackathon_2025.Services;

namespace Hackathon_2025.Tests.Services;

[TestClass]
public class PngImageInspectorTests
{
    /// <summary>Builds a minimal valid PNG (filter 0 rows, zlib IDAT). CRCs are zero: the inspector doesn't check them.</summary>
    private static byte[] Png(int width, int height, byte colorType, Func<int, int, byte[]> pixel, byte bitDepth = 8)
    {
        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            for (var x = 0; x < width; x++) raw.Write(pixel(x, y));
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(raw.ToArray());

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = bitDepth;
        header[9] = colorType;

        using var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        stream.Write(Encoding.ASCII.GetBytes(type));
        stream.Write(data);
        stream.Write(new byte[4]);
    }

    [TestMethod]
    public void All_Black_Image_Is_Suspicious()
    {
        var ok = PngImageInspector.TryAnalyze(Png(4, 4, 2, (_, _) => new byte[] { 0, 0, 0 }), out var stats);

        Assert.IsTrue(ok);
        Assert.AreEqual(4, stats!.Width);
        Assert.AreEqual(4, stats.Height);
        Assert.AreEqual(0, stats.AverageBrightness, 0.001);
        Assert.AreEqual(1.0, stats.NearBlackRatio, 0.001);
        Assert.IsTrue(stats.IsSuspiciouslyDark);
    }

    [TestMethod]
    public void White_Image_Is_Not_Suspicious()
    {
        PngImageInspector.TryAnalyze(Png(4, 4, 2, (_, _) => new byte[] { 255, 255, 255 }), out var stats);

        // Rec. 709 weights sum to 1, but in floating point white lands at 254.999… and the byte cast truncates to 254.
        Assert.AreEqual(255, stats!.AverageBrightness, 1.0);
        Assert.IsFalse(stats.IsSuspiciouslyDark);
    }

    [TestMethod]
    public void Half_Black_Half_White_Is_Not_Suspicious()
    {
        PngImageInspector.TryAnalyze(Png(4, 4, 0, (x, _) => new[] { x < 2 ? (byte)0 : (byte)255 }), out var stats);
        Assert.IsFalse(stats!.IsSuspiciouslyDark);
    }

    [TestMethod]
    public void Non_Png_Bytes_Are_Rejected() => Assert.IsFalse(PngImageInspector.TryAnalyze(Encoding.UTF8.GetBytes("not a png"), out _));

    [TestMethod]
    public void Truncated_Png_Is_Rejected() => Assert.IsFalse(PngImageInspector.TryAnalyze(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, out _));

    [TestMethod]
    public void Sixteen_Bit_Png_Is_Not_Analyzed()
        => Assert.IsFalse(PngImageInspector.TryAnalyze(Png(2, 2, 2, (_, _) => new byte[] { 0, 0, 0 }, bitDepth: 16), out _));
}
