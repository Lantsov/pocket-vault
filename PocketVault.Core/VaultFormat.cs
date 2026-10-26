using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace PocketVault.Core;

public static class VaultFormat
{
    public const int MaxFileSize = 128 * 1024 * 1024;
    public const int MaxZipSize = MaxFileSize + 1024 * 1024;
    public const int HeaderSize = 52;
    public const int TagSize = 16;
    public const int Iterations = 600_000;
    private static ReadOnlySpan<byte> Magic => "PVLT\r\n\x1a\n"u8;

    public static byte[] Encrypt(string fileName, ReadOnlySpan<byte> data, string password)
    {
        ValidatePassword(password);
        ValidateFileName(fileName);
        if (data.Length > MaxFileSize) throw new VaultException("Размер исходного файла превышает 128 МиБ.");
        using var zip = new MemoryStream();
        byte[] key = new byte[32];
        try
        {
            using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, true, Encoding.UTF8))
            using (var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal).Open())
                entry.Write(data);
            int zipLength = checked((int)zip.Length);
            if (zipLength > MaxZipSize) throw new VaultException("Архив превышает допустимый размер.");
            byte[] result = new byte[HeaderSize + zipLength + TagSize];
            Span<byte> header = result.AsSpan(0, HeaderSize);
            Magic.CopyTo(header);
            header[8] = 1; // версия
            header[9] = 1; // PBKDF2-HMAC-SHA256
            header[10] = 1; // AES-256-GCM
            header[11] = 0; // зарезервировано
            BinaryPrimitives.WriteInt32LittleEndian(header[12..], Iterations);
            RandomNumberGenerator.Fill(header.Slice(16, 16));
            RandomNumberGenerator.Fill(header.Slice(32, 12));
            BinaryPrimitives.WriteInt64LittleEndian(header[44..], zipLength);
            Rfc2898DeriveBytes.Pbkdf2(password, header.Slice(16, 16), key, Iterations, HashAlgorithmName.SHA256);
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(header.Slice(32, 12), zip.GetBuffer().AsSpan(0, zipLength),
                result.AsSpan(HeaderSize, zipLength), result.AsSpan(HeaderSize + zipLength, TagSize), header);
            return result;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(zip.GetBuffer());
        }
    }

    public static RestoredFile Decrypt(ReadOnlySpan<byte> container, string password)
    {
        ValidatePassword(password);
        int length = ValidateHeader(container, container.Length);
        byte[] zip = new byte[length];
        byte[] key = new byte[32];
        try
        {
            ReadOnlySpan<byte> header = container[..HeaderSize];
            Rfc2898DeriveBytes.Pbkdf2(password, header.Slice(16, 16), key, Iterations, HashAlgorithmName.SHA256);
            using var aes = new AesGcm(key, TagSize);
            try
            {
                aes.Decrypt(header.Slice(32, 12), container.Slice(HeaderSize, length),
                    container.Slice(HeaderSize + length, TagSize), zip, header);
            }
            catch (AuthenticationTagMismatchException ex)
            {
                throw new VaultException("Неверный пароль или файл повреждён.", ex);
            }
            return Unpack(zip);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(zip);
        }
    }

    // Проверяется до выделения памяти и затратного получения ключа.
    public static int ValidateHeader(ReadOnlySpan<byte> header, long totalLength)
    {
        if (header.Length < HeaderSize || totalLength < HeaderSize + TagSize || !header[..8].SequenceEqual(Magic))
            throw new VaultException("Это не контейнер PocketVault либо файл повреждён или обрезан.");
        if (header[8] != 1) throw new VaultException("Неподдерживаемая версия формата PocketVault.");
        if (header[9] != 1 || header[10] != 1 || header[11] != 0 ||
            BinaryPrimitives.ReadInt32LittleEndian(header[12..]) != Iterations)
            throw new VaultException("Неподдерживаемые или повреждённые параметры шифрования.");
        long length = BinaryPrimitives.ReadInt64LittleEndian(header[44..]);
        if (length < 22 || length > MaxZipSize || totalLength != HeaderSize + length + TagSize)
            throw new VaultException("Контейнер повреждён, обрезан или превышает допустимый размер.");
        return (int)length;
    }

    private static RestoredFile Unpack(byte[] zip)
    {
        byte[]? data = null;
        try
        {
            using var stream = new MemoryStream(zip, false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, false, Encoding.UTF8);
            if (archive.Entries.Count != 1) throw new VaultException("Архив должен содержать ровно один файл.");
            var entry = archive.Entries[0];
            ValidateFileName(entry.FullName);
            if (entry.Length < 0 || entry.Length > MaxFileSize)
                throw new VaultException("Размер восстановленного файла превышает 128 МиБ.");
            data = new byte[(int)entry.Length];
            using var source = entry.Open();
            source.ReadExactly(data);
            if (source.ReadByte() != -1) throw new VaultException("Неверный размер файла в архиве.");
            var result = new RestoredFile(entry.FullName, data);
            data = null;
            return result;
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentException)
        {
            throw new VaultException("Зашифрованный архив повреждён.", ex);
        }
        finally
        {
            if (data is not null) CryptographicOperations.ZeroMemory(data);
        }
    }

    public static void ValidateFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name is "." or ".." ||
            name.EndsWith('.') || name.EndsWith(' ') || name.Any(c => c < 32 || "<>:\"/\\|?*".Contains(c)))
            throw new VaultException("Архив содержит недопустимое имя файла.");
        string stem = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
            (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
            "123456789¹²³".Contains(stem[3])))
            throw new VaultException("Архив содержит зарезервированное Windows имя файла.");
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password)) throw new VaultException("Введите непустой пароль.");
    }
}

public sealed class RestoredFile(string name, byte[] data) : IDisposable
{
    public string Name { get; } = name;
    public byte[] Data { get; } = data;
    public void Dispose() => CryptographicOperations.ZeroMemory(Data);
}
