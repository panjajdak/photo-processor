namespace PhotoProcessor.Core.Tests.Support;

using System.Buffers.Binary;
using System.Text;

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
        var tiff = Exif(make, model, dateTime, dateTimeOriginal, dateTimeDigitized, subSecondOriginal);

        using var jpeg = new MemoryStream();
        jpeg.Write([0xFF, 0xD8, 0xFF, 0xE1]);
        WriteUInt16BigEndian(jpeg, (ushort)(2 + 6 + tiff.Length));
        jpeg.Write("Exif\0\0"u8);
        jpeg.Write(tiff);
        jpeg.Write([0xFF, 0xD9]);
        return jpeg.ToArray();
    }

    public static byte[] Tiff(string? model = null, DateTime? dateTimeOriginal = null)
        => Exif(null, model, null, dateTimeOriginal, null, null);

    public static byte[] WebP(string? model = null, DateTime? dateTimeOriginal = null)
    {
        var exif = Exif(null, model, null, dateTimeOriginal, null, null);

        var vp8x = new byte[10];
        const byte ExifPresentFlag = 0x08;
        vp8x[0] = ExifPresentFlag;

        byte[] chunks = [.. Chunk("VP8X", vp8x), .. Chunk("EXIF", exif)];
        var riff = new byte[12];
        Encoding.ASCII.GetBytes("RIFF", riff);
        BinaryPrimitives.WriteUInt32LittleEndian(riff.AsSpan(4), (uint)(4 + chunks.Length));
        Encoding.ASCII.GetBytes("WEBP", riff.AsSpan(8));
        return [.. riff, .. chunks];

        static byte[] Chunk(string type, byte[] payload)
        {
            var chunk = new byte[8 + payload.Length + (payload.Length % 2)];
            Encoding.ASCII.GetBytes(type, chunk);
            BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(4), (uint)payload.Length);
            payload.CopyTo(chunk, 8);
            return chunk;
        }
    }

    public static byte[] Mov(DateTime created) => Mp4(created, "qt  ");

    public static byte[] Mp4(DateTime created) => Mp4(created, "isom");

    private static byte[] Mp4(DateTime created, string brand)
    {
        var seconds = (uint)(created - new DateTime(1904, 1, 1)).TotalSeconds;

        var mvhd = new byte[100];
        const int CreationTime = 4, ModificationTime = 8, TimeScale = 12, PreferredRate = 20, PreferredVolume = 24;
        const int MatrixA = 36, MatrixD = 52, MatrixW = 68, NextTrackId = 96;
        const uint FixedPointOne = 0x00010000;

        BinaryPrimitives.WriteUInt32BigEndian(mvhd.AsSpan(CreationTime), seconds);
        BinaryPrimitives.WriteUInt32BigEndian(mvhd.AsSpan(ModificationTime), seconds);
        BinaryPrimitives.WriteUInt32BigEndian(mvhd.AsSpan(TimeScale), 1000);
        BinaryPrimitives.WriteUInt32BigEndian(mvhd.AsSpan(PreferredRate), FixedPointOne);
        BinaryPrimitives.WriteUInt16BigEndian(mvhd.AsSpan(PreferredVolume), 0x0100);
        BinaryPrimitives.WriteUInt32BigEndian(mvhd.AsSpan(MatrixA), FixedPointOne);
        BinaryPrimitives.WriteUInt32BigEndian(mvhd.AsSpan(MatrixD), FixedPointOne);
        BinaryPrimitives.WriteUInt32BigEndian(mvhd.AsSpan(MatrixW), 0x40000000);
        BinaryPrimitives.WriteUInt32BigEndian(mvhd.AsSpan(NextTrackId), 1);

        var mvhdBox = Box("mvhd", mvhd);
        var ftyp = Box("ftyp", [.. Encoding.ASCII.GetBytes(brand), 0, 0, 2, 0, .. Encoding.ASCII.GetBytes(brand)]);
        return [.. ftyp, .. Box("moov", mvhdBox)];
    }

    private static byte[] Exif(string? make, string? model, DateTime? dateTime, DateTime? dateTimeOriginal, DateTime? dateTimeDigitized, string? subSecondOriginal)
    {
        var ifd0 = new List<(ushort Tag, ushort Type, byte[] Data)>();
        if (make != null) ifd0.Add((0x010F, TypeAscii, Ascii(make)));
        if (model != null) ifd0.Add((0x0110, TypeAscii, Ascii(model)));
        if (dateTime != null) ifd0.Add((0x0132, TypeAscii, ExifDate(dateTime.Value)));

        var subIfd = new List<(ushort Tag, ushort Type, byte[] Data)>();
        if (dateTimeOriginal != null) subIfd.Add((0x9003, TypeAscii, ExifDate(dateTimeOriginal.Value)));
        if (dateTimeDigitized != null) subIfd.Add((0x9004, TypeAscii, ExifDate(dateTimeDigitized.Value)));
        if (subSecondOriginal != null) subIfd.Add((0x9291, TypeAscii, Ascii(subSecondOriginal)));

        return Tiff(ifd0, subIfd);
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

        const uint NoNextIfd = 0;
        WriteUInt32LittleEndian(stream, NoNextIfd);
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
