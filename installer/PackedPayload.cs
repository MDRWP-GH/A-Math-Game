using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

/// <summary>
/// Self-extracting payload appended after Setup.exe:
/// [stub PE][zip bytes][int64 offset][8-byte magic AMTHZIP1]
/// </summary>
internal static class PackedPayload
{
    public const string Magic = "AMTHZIP1";
    public const int FooterSize = 16;

    public static bool HasPayload()
    {
        long offset;
        long length;
        return TryGetRange(ThisExePath(), out offset, out length);
    }

    public static void ExtractTo(string destDir, Action<string> report)
    {
        if (report == null)
            report = delegate { };

        string exePath = ThisExePath();
        long offset;
        long length;
        if (!TryGetRange(exePath, out offset, out length))
        {
            throw new InvalidOperationException(I18n.T(
                "This Setup.exe has no game payload. Build a Windows player from Unity to create a complete installer.",
                "Setup.exe นี้ไม่มีไฟล์เกมในตัว ให้ Build เกม Windows จาก Unity เพื่อสร้างตัวติดตั้งที่สมบูรณ์"));
        }

        Directory.CreateDirectory(destDir);
        string destRoot = Path.GetFullPath(destDir).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;

        using (FileStream exe = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (Stream slice = new OffsetStream(exe, offset, length))
        using (ZipArchive archive = new ZipArchive(slice, ZipArchiveMode.Read))
        {
            int total = archive.Entries.Count;
            int index = 0;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                index++;
                string relative = (entry.FullName ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
                if (string.IsNullOrEmpty(relative))
                    continue;

                string destPath = Path.GetFullPath(Path.Combine(destDir, relative));
                bool isDir = relative.EndsWith(Path.DirectorySeparatorChar.ToString()) || string.IsNullOrEmpty(entry.Name);
                if (isDir)
                {
                    if (!destPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                        destPath += Path.DirectorySeparatorChar;
                    if (!destPath.StartsWith(destRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Invalid payload entry.");
                    Directory.CreateDirectory(destPath);
                    continue;
                }

                if (!destPath.StartsWith(destRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Invalid payload entry.");

                if (string.Equals(Path.GetFileName(destPath), AppInfo.SetupExeName, StringComparison.OrdinalIgnoreCase))
                    continue;

                string destFolder = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destFolder))
                    Directory.CreateDirectory(destFolder);

                if (index == 1 || (index % 8) == 0 || index == total)
                {
                    report(I18n.T(
                        "Extracting files… " + index + "/" + total,
                        "กำลังแตกไฟล์… " + index + "/" + total));
                }

                using (Stream input = entry.Open())
                using (FileStream output = File.Create(destPath))
                    CopyStream(input, output);
            }
        }
    }

    public static bool TryGetRange(string exePath, out long offset, out long length)
    {
        offset = 0;
        length = 0;
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            return false;

        using (FileStream fs = File.OpenRead(exePath))
        {
            if (fs.Length < FooterSize + 1)
                return false;

            fs.Seek(-FooterSize, SeekOrigin.End);
            byte[] footer = new byte[FooterSize];
            if (!ReadExact(fs, footer, FooterSize))
                return false;

            if (Encoding.ASCII.GetString(footer, 8, 8) != Magic)
                return false;

            offset = BitConverter.ToInt64(footer, 0);
            if (offset < 0 || offset >= fs.Length - FooterSize)
                return false;

            length = fs.Length - FooterSize - offset;
            return length > 0;
        }
    }

    private static string ThisExePath()
    {
        string location = Assembly.GetExecutingAssembly().Location;
        if (!string.IsNullOrEmpty(location))
            return Path.GetFullPath(location);
        return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppInfo.SetupExeName));
    }

    private static void CopyStream(Stream input, Stream output)
    {
        byte[] buffer = new byte[81920];
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            output.Write(buffer, 0, read);
    }

    private static bool ReadExact(Stream stream, byte[] buffer, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = stream.Read(buffer, offset, count - offset);
            if (read <= 0)
                return false;
            offset += read;
        }
        return true;
    }
}

internal sealed class OffsetStream : Stream
{
    private readonly Stream _inner;
    private readonly long _start;
    private readonly long _length;
    private long _position;

    public OffsetStream(Stream inner, long start, long length)
    {
        _inner = inner;
        _start = start;
        _length = length;
        _position = 0;
    }

    public override bool CanRead { get { return true; } }
    public override bool CanSeek { get { return true; } }
    public override bool CanWrite { get { return false; } }
    public override long Length { get { return _length; } }

    public override long Position
    {
        get { return _position; }
        set { Seek(value, SeekOrigin.Begin); }
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= _length)
            return 0;
        long remaining = _length - _position;
        if (count > remaining)
            count = (int)remaining;
        _inner.Position = _start + _position;
        int read = _inner.Read(buffer, offset, count);
        _position += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long next;
        if (origin == SeekOrigin.Begin)
            next = offset;
        else if (origin == SeekOrigin.Current)
            next = _position + offset;
        else if (origin == SeekOrigin.End)
            next = _length + offset;
        else
            throw new ArgumentOutOfRangeException("origin");

        if (next < 0)
            throw new IOException();
        if (next > _length)
            next = _length;
        _position = next;
        return _position;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
}
