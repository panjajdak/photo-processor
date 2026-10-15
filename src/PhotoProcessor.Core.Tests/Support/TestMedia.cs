namespace PhotoProcessor.Core.Tests.Support;

using System.Buffers.Binary;
using System.Text;

/// <summary>
/// Builds minimal but valid JPEG and MP4 files carrying just the metadata the tests need.
/// </summary>
internal static class TestMedia
{
    private const ushort TypeAscii = 2;
    private const ushort TypeLong = 4;

    public static byte[] Jpeg(
        string? make = null,
        string? model = null,
        DateTime? dateTime = null,
        DateTime? dateTimeOriginal = null,
        DateTime? dateTimeDigitized = null,
        string? subSecondOriginal = null)
    {
        var ifd0 = new List<(ushort Tag, ushort Type, byte[] Data)>();
        if (make != null) ifd0.Add((0x010F, TypeAscii, Ascii(make)));
        if (model != null) ifd0.Add((0x0110, TypeAscii, Ascii(model)));
        if (dateTime != null) ifd0.Add((0x0132, TypeAscii, ExifDate(dateTime.Value)));

        var subIfd = new List<(ushort Tag, ushort Type, byte[] Data)>();
        if (dateTimeOriginal != null) subIfd.Add((0x9003, TypeAscii, ExifDate(dateTimeOriginal.Value)));
        if (dateTimeDigitized != null) subIfd.Add((0x9004, TypeAscii, ExifDate(dateTimeDigitized.Value)));
        if (subSecondOriginal != null) subIfd.Add((0x9291, TypeAscii, Ascii(subSecondOriginal)));

        var tiff = Tiff(ifd0, subIfd);

        using var jpeg = new MemoryStream();
        jpeg.Write([0xFF, 0xD8, 0xFF, 0xE1]);
        WriteUInt16BigEndian(jpeg, (ushort)(2 + 6 + tiff.Length));
        jpeg.Write("Exif\0\0"u8);
        jpeg.Write(tiff);
        jpeg.Write([0xFF, 0xD9]);
        return jpeg.ToArray();
    }

    /// <summary>MP4 whose movie header creation time is <paramref name="created"/> (seconds since 1904, as stored).</summary>
    public static byte[] Mp4(DateTime created)
    {
        var seconds = (uint)(created - new DateTime(1904, 1, 1)).TotalSeconds;

        var mvhd = new byte[100];
        BinaryPrimitives.WriteUInt32BigEndian(mvhd.AsSpan(4), seconds);   // creation time
        BinaryPrimitives.WriteUInt32BigEndian(mvhd.AsSpan(8), seconds);   // modification time
        BinaryPrimitives.WriteUInt32BigEndian(mvhd.AsSpan(12), 1000);     // time scale
        BinaryPrimitives.WriteUInt32BigEndian(mvhd.AsSpan(20), 0x00010000); // preferred rate 1.0
        BinaryPrimitives.WriteUInt16BigEndian(mvhd.AsSpan(24), 0x0100);   // preferred volume 1.0
        BinaryPrimitives.WriteUInt32BigEndian(mvhd.AsSpan(36), 0x00010000); // identity matrix
        BinaryPrimitives.WriteUInt32BigEndian(mvhd.AsSpan(52), 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(mvhd.AsSpan(68), 0x40000000);
        BinaryPrimitives.WriteUInt32BigEndian(mvhd.AsSpan(96), 1);        // next track id

        var mvhdBox = Box("mvhd", mvhd);
        var ftyp = Box("ftyp", [.. "isom"u8, 0, 0, 2, 0, .. "isommp41"u8]);
        return [.. ftyp, .. Box("moov", mvhdBox)];
    }

    private static byte[] Box(string type, byte[] payload)
    {
        var box = new byte[8 + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(box, (uint)box.Length);
        Encoding.ASCII.GetBytes(type, box.AsSpan(4));
        payload.CopyTo(box, 8);
        return box;
    }

    private static byte[] Tiff(List<(ushort Tag, ushort Type, byte[] Data)> ifd0, List<(ushort Tag, ushort Type, byte[] Data)> subIfd)
    {
        const int HeaderSize = 8;
        var hasSubIfd = subIfd.Count > 0;
        var ifd0Entries = ifd0.Count + (hasSubIfd ? 1 : 0);
        var ifd0Size = IfdSize(ifd0Entries) + ExternalDataSize(ifd0);
        var subIfdOffset = HeaderSize + ifd0Size;

        if (hasSubIfd)
        {
            var pointer = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(pointer, (uint)subIfdOffset);
            ifd0.Add((0x8769, TypeLong, pointer));
        }

        using var stream = new MemoryStream();
        stream.Write("II"u8);
        WriteUInt16LittleEndian(stream, 42);
        WriteUInt32LittleEndian(stream, HeaderSize);
        WriteIfd(stream, ifd0, HeaderSize);
        if (hasSubIfd)
        {
            WriteIfd(stream, subIfd, subIfdOffset);
        }

        return stream.ToArray();
    }

    private static void WriteIfd(MemoryStream stream, List<(ushort Tag, ushort Type, byte[] Data)> entries, int offset)
    {
        entries.Sort((a, b) => a.Tag.CompareTo(b.Tag));
        var dataOffset = offset + IfdSize(entries.Count);
        var external = new MemoryStream();

        WriteUInt16LittleEndian(stream, (ushort)entries.Count);
        foreach (var (tag, type, data) in entries)
        {
            WriteUInt16LittleEndian(stream, tag);
            WriteUInt16LittleEndian(stream, type);
            WriteUInt32LittleEndian(stream, type == TypeLong ? (uint)(data.Length / 4) : (uint)data.Length);

            if (data.Length <= 4)
            {
                var inline = new byte[4];
                data.CopyTo(inline, 0);
                stream.Write(inline);
            }
            else
            {
                WriteUInt32LittleEndian(stream, (uint)(dataOffset + external.Length));
                external.Write(data);
                if (external.Length % 2 == 1) external.WriteByte(0);
            }
        }

        WriteUInt32LittleEndian(stream, 0); // no next IFD
        external.WriteTo(stream);
    }

    private static int IfdSize(int entryCount) => 2 + (12 * entryCount) + 4;

    private static int ExternalDataSize(IEnumerable<(ushort Tag, ushort Type, byte[] Data)> entries)
        => entries.Where(e => e.Data.Length > 4).Sum(e => e.Data.Length + (e.Data.Length % 2));

    private static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value + "\0");

    private static byte[] ExifDate(DateTime date) => Ascii(date.ToString("yyyy:MM:dd HH:mm:ss"));

    private static void WriteUInt16BigEndian(Stream stream, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteUInt16LittleEndian(Stream stream, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteUInt32LittleEndian(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }
}
