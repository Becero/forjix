using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
namespace Forjix.Application.Common;

// Same direct PDF object/xref mechanism used by the reports, extended for pages and tenant logos.
internal static class PdfDocument
{
    public sealed record Logo(int Width, int Height, byte[] Data, string Filter, string ColorSpace);
    public static string Text(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    public static byte[] Create(IReadOnlyList<string> pages, Logo? logo = null)
    {
        var objects = new List<byte[]> { Array.Empty<byte>(), Array.Empty<byte>(), Bytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>") };
        var imageId = 0;
        if (logo is not null)
        {
            imageId = objects.Count + 1;
            objects.Add(Stream($" /Type /XObject /Subtype /Image /Width {logo.Width} /Height {logo.Height} /ColorSpace /{logo.ColorSpace} /BitsPerComponent 8 /Filter /{logo.Filter}", logo.Data));
        }
        var pageIds = new List<int>();
        foreach (var content in pages)
        {
            var pageId = objects.Count + 1; pageIds.Add(pageId); var streamId = pageId + 1;
            var resources = $"<< /Font << /F1 3 0 R >> {(imageId == 0 ? "" : $"/XObject << /Logo {imageId} 0 R >>")} >>";
            objects.Add(Bytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources {resources} /Contents {streamId} 0 R >>"));
            objects.Add(Stream("", Bytes(content)));
        }
        objects[0] = Bytes("<< /Type /Catalog /Pages 2 0 R >>");
        objects[1] = Bytes($"<< /Type /Pages /Kids [{string.Join(" ", pageIds.Select(x => $"{x} 0 R"))}] /Count {pageIds.Count} >>");
        using var output = new MemoryStream(); output.Write(Bytes("%PDF-1.4\n"));
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(output.Position); output.Write(Bytes($"{i + 1} 0 obj\n")); output.Write(objects[i]); output.Write(Bytes("\nendobj\n"));
        }
        var xref = output.Position;
        output.Write(Bytes($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n"));
        foreach (var offset in offsets.Skip(1)) output.Write(Bytes($"{offset:0000000000} 00000 n \n"));
        output.Write(Bytes($"trailer << /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF"));
        return output.ToArray();
    }
    private static byte[] Bytes(string value) => Encoding.Latin1.GetBytes(value);
    private static byte[] Stream(string attributes, byte[] data)
    {
        using var stream = new MemoryStream(); stream.Write(Bytes($"<< /Length {data.Length}{attributes} >>\nstream\n"));
        stream.Write(data); stream.Write(Bytes("\nendstream")); return stream.ToArray();
    }
    public static Logo? ReadLogo(byte[]? bytes)
    {
        if (bytes is null) return null;
        try
        {
            if (bytes.Length > 3 && bytes[0] == 255 && bytes[1] == 216)
            {
                var i = 2;
                while (i + 8 < bytes.Length)
                {
                    if (bytes[i++] != 255) continue;
                    var marker = bytes[i++]; if (marker == 255) { i--; continue; }
                    if (marker is 216 or 217) continue;
                    var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(i));
                    if (marker is 192 or 193 or 194)
                    {
                        var jpegWidth = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(i + 5));
                        var jpegHeight = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(i + 3));
                        if (jpegWidth == 0 || jpegHeight == 0 || (long)jpegWidth * jpegHeight > 4_000_000 || bytes[i + 2] != 8 || bytes[i + 7] is not (1 or 3)) throw new InvalidDataException();
                        return new(jpegWidth, jpegHeight, bytes, "DCTDecode", bytes[i + 7] == 1 ? "DeviceGray" : "DeviceRGB");
                    }
                    i += length;
                }
            }
            if (bytes.Length < 33 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 })) throw new InvalidDataException();
            var width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16)); var height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20));
            var depth = bytes[24]; var color = bytes[25];
            if (width <= 0 || height <= 0 || (long)width * height > 4_000_000 || depth != 8 || bytes[28] != 0 || color is not (0 or 2 or 3 or 4 or 6)) throw new InvalidDataException();
            using var compressed = new MemoryStream(); byte[]? palette = null;
            var offset = 8;
            while (offset + 12 <= bytes.Length)
            {
                var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset)); if (length < 0 || length > bytes.Length - offset - 12) throw new InvalidDataException();
                var type = Encoding.ASCII.GetString(bytes, offset + 4, 4);
                if (type == "IDAT") compressed.Write(bytes.AsSpan(offset + 8, length));
                if (type == "PLTE") palette = bytes.AsSpan(offset + 8, length).ToArray();
                offset += length + 12;
            }
            var channels = color switch { 0 or 3 => 1, 2 => 3, 4 => 2, _ => 4 };
            var stride = checked(width * channels);
            compressed.Position = 0; using var decoder = new ZLibStream(compressed, CompressionMode.Decompress);
            var row = new byte[stride]; var previous = new byte[stride]; var rgb = new byte[checked(width * height * 3)];
            for (var y = 0; y < height; y++)
            {
                var filter = decoder.ReadByte(); if (filter is < 0 or > 4) throw new InvalidDataException(); decoder.ReadExactly(row);
                for (var x = 0; x < stride; x++)
                {
                    int left = x >= channels ? row[x - channels] : 0, up = previous[x], upperLeft = x >= channels ? previous[x - channels] : 0;
                    row[x] = unchecked((byte)(row[x] + (filter switch { 0 => 0, 1 => left, 2 => up, 3 => (left + up) / 2, _ => Paeth(left, up, upperLeft) })));
                }
                for (var x = 0; x < width; x++)
                {
                    var source = x * channels; var target = (y * width + x) * 3;
                    var alpha = color is 4 or 6 ? row[source + channels - 1] : 255;
                    for (var c = 0; c < 3; c++)
                    {
                        var value = color == 3 ? palette?[row[source] * 3 + c] ?? throw new InvalidDataException() : row[source + (color is 0 or 4 ? 0 : c)];
                        rgb[target + c] = (byte)((value * alpha + 255 * (255 - alpha)) / 255);
                    }
                }
                (row, previous) = (previous, row);
            }
            using var encoded = new MemoryStream(); using (var encoder = new ZLibStream(encoded, CompressionLevel.Optimal, true)) encoder.Write(rgb);
            return new(width, height, encoded.ToArray(), "FlateDecode", "DeviceRGB");
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or ArgumentException or IndexOutOfRangeException or OverflowException)
        {
            throw new RequestValidationException("Para exportar o logotipo no PDF, utilize PNG de 8 bits não entrelaçado ou JPEG RGB. Revise o arquivo nas configurações da empresa.");
        }
    }
    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c; var pa = Math.Abs(p - a); var pb = Math.Abs(p - b); var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
