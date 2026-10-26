using System.IO;
using System.Security.Cryptography;
using System.Text;
using PocketVault.Core;

namespace PocketVault.Tests;

internal static class AndroidInteropChecks
{
    internal const string Password = "Stage2-Test-2026";
    private const string Name = "Обмен #1 (a+b) & тест.bin";
    private static byte[] Sample => [.. Encoding.UTF8.GetBytes("Synthetic PocketVault Android / Windows exchange.\nПривет!\n"), .. Enumerable.Range(0, 256).Select(i => (byte)i)];

    internal static void Prepare(string directory)
    {
        if (Directory.Exists(directory)) throw new IOException("Use a fresh synthetic test directory.");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, Name), Sample);
        VaultFiles.Encrypt(Path.Combine(directory, Name), Path.Combine(directory, "windows.pvlt"), Password);
        Console.WriteLine($"Synthetic files prepared. Public password: {Password}");
    }

    internal static void Verify(string directory)
    {
        string windowsOutput = Path.Combine(directory, "windows-restored");
        Directory.CreateDirectory(windowsOutput);
        string restored = VaultFiles.Decrypt(Path.Combine(directory, "android.pvlt"), windowsOutput, Password);
        byte[] expected = Sample;
        if (Path.GetFileName(restored) != Name || !File.ReadAllBytes(restored).AsSpan().SequenceEqual(expected))
            throw new InvalidOperationException("Android → Windows mismatch.");
        if (!File.ReadAllBytes(Path.Combine(directory, "android-restored.bin")).AsSpan().SequenceEqual(expected))
            throw new InvalidOperationException("Windows → Android mismatch.");
        Console.WriteLine($"PASS Android → Windows / Windows → Android; exact bytes: {expected.Length}; SHA256: {Convert.ToHexString(SHA256.HashData(expected))}");
    }
}
