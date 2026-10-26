using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace PocketVault.Core;

public static class VaultFiles
{
    public static string SuggestContainerName() => $"vault-{DateTime.Now:yyyyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant()}.pvlt";

    public static void Encrypt(string inputPath, string outputPath, string password, bool deleteOriginal = false)
    {
        EnsureDifferentPaths(inputPath, outputPath);
        // При удалении держим один Windows handle с правом DELETE: другой процесс
        // не сможет подменить исходник между проверкой и удалением.
        using var input = OpenSource(inputPath, deleteOriginal);
        byte[] original = ReadBounded(input, VaultFormat.MaxFileSize, "Размер исходного файла превышает 128 МиБ.");
        try
        {
            string name = Path.GetFileName(inputPath);
            byte[] container = VaultFormat.Encrypt(name, original, password);
            AtomicWrite(outputPath, container);
            if (deleteOriginal)
            {
                using var savedStream = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                byte[] saved = ReadContainer(savedStream);
                using var restored = VaultFormat.Decrypt(saved, password);
                if (restored.Name != name || !CryptographicOperations.FixedTimeEquals(original, restored.Data))
                    throw new VaultException("Проверка контейнера не пройдена. Исходный файл сохранён.");
                var disposition = new FileDisposition { DeleteFile = true };
                if (!SetFileInformationByHandle(input.SafeFileHandle, 4, ref disposition, 1))
                    throw new VaultException("Контейнер создан и проверен, но исходный файл удалить не удалось.", new Win32Exception(Marshal.GetLastWin32Error()));
            }
        }
        finally { CryptographicOperations.ZeroMemory(original); }
    }

    public static string Decrypt(string inputPath, string outputDirectory, string password)
    {
        byte[] container = ReadContainer(inputPath);
        using var restored = VaultFormat.Decrypt(container, password);
        string outputPath = Path.Combine(Path.GetFullPath(outputDirectory), restored.Name);
        EnsureDifferentPaths(inputPath, outputPath);
        AtomicWrite(outputPath, restored.Data);
        return outputPath;
    }

    public static byte[] ReadContainer(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ReadContainer(stream);
    }

    private static byte[] ReadContainer(FileStream stream)
    {
        Span<byte> header = stackalloc byte[VaultFormat.HeaderSize];
        if (stream.Length < header.Length) throw new VaultException("Контейнер обрезан.");
        stream.ReadExactly(header);
        VaultFormat.ValidateHeader(header, stream.Length);
        stream.Position = 0;
        return ReadBounded(stream, VaultFormat.MaxZipSize + VaultFormat.HeaderSize + VaultFormat.TagSize, "Контейнер слишком большой.");
    }

    private static byte[] ReadBounded(FileStream stream, int max, string error)
    {
        if (stream.Length > max) throw new VaultException(error);
        byte[] data = new byte[checked((int)stream.Length)];
        try
        {
            stream.ReadExactly(data);
            if (stream.ReadByte() != -1) throw new VaultException("Файл изменился во время чтения.");
            return data;
        }
        catch { CryptographicOperations.ZeroMemory(data); throw; }
    }

    private static void AtomicWrite(string destination, ReadOnlySpan<byte> data)
    {
        string fullPath = Path.GetFullPath(destination);
        if (File.Exists(fullPath) || Directory.Exists(fullPath))
            throw new VaultException("Файл с таким именем уже существует. Выберите другое имя или папку.");
        string temp = Path.Combine(Path.GetDirectoryName(fullPath)!, $".pvlt-{Guid.NewGuid():N}.tmp");
        bool created = false;
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                stream.Write(data);
                stream.Flush(true);
            }
            File.Move(temp, fullPath, false);
        }
        finally
        {
            if (created && File.Exists(temp)) File.Delete(temp);
        }
    }

    private static void EnsureDifferentPaths(string input, string output)
    {
        if (string.Equals(Path.GetFullPath(input), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
            throw new VaultException("Исходный и выходной файлы должны различаться.");
    }

    private static FileStream OpenSource(string path, bool needsDelete)
    {
        if (!needsDelete) return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (!OperatingSystem.IsWindows()) throw new VaultException("Удаление исходника поддерживается только в Windows.");
        SafeFileHandle handle = CreateFileW(Path.GetFullPath(path), 0x80000000 | 0x00010000, 1, IntPtr.Zero, 3, 0x08000000, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new VaultException("Не удалось открыть исходный файл для проверенного удаления.", new Win32Exception(error));
        }
        try { return new FileStream(handle, FileAccess.Read); }
        catch { handle.Dispose(); throw; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDisposition { [MarshalAs(UnmanagedType.U1)] public bool DeleteFile; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int infoClass, ref FileDisposition info, uint size);
}
