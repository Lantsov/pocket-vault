using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using PocketVault.Core;

namespace PocketVault.Android.Diagnostics;

internal sealed record SmokeCheck(string Name, bool Success, string Detail);
internal sealed record SmokeReport(string RunId, DateTimeOffset CompletedAt, string Runtime,
    int AndroidApi, bool Success, IReadOnlyList<SmokeCheck> Checks);

internal static class CryptoSmokeTest
{
    private const string Password = "Публичный Android smoke-test 🗝";
    private const string FileName = "Android проверка 🗝.bin";

    internal static SmokeReport Run(string runId)
    {
        // A host-side execution must never produce an Android compatibility report.
        if (!OperatingSystem.IsAndroid())
            throw new PlatformNotSupportedException("Smoke-test должен выполняться на Android runtime.");

        var checks = new List<SmokeCheck>();
        byte[] sample = [.. Encoding.UTF8.GetBytes("PocketVault Android\nПривет!\n"), 0, 1, 127, 128, 255];
        byte[]? container = null;

        void RunCheck(string name, Action action)
        {
            try { action(); checks.Add(new(name, true, "OK")); }
            catch (Exception ex) { checks.Add(new(name, false, $"{ex.GetType().Name}: {ex.Message}")); }
        }

        RunCheck("AES-256-GCM: известный вектор и AAD", CheckAesGcm);
        RunCheck("PBKDF2-HMAC-SHA256: RFC 7914", CheckPbkdf2);
        RunCheck("ZIP: точные имя и байты", () => CheckZip(sample));
        RunCheck("Создание .pvlt v1: заголовок 52 байта, 600 000 итераций", () =>
        {
            Require(VaultFormat.HeaderSize == 52 && VaultFormat.TagSize == 16 && VaultFormat.Iterations == 600_000,
                "Изменились параметры формата.");
            container = VaultFormat.Encrypt(FileName, sample, Password);
            Require(container.AsSpan(0, 8).SequenceEqual("PVLT\r\n\x1a\n"u8) && container[8] == 1 &&
                container[9] == 1 && container[10] == 1 && container[11] == 0, "Неверный заголовок.");
            Require(BinaryPrimitives.ReadInt32LittleEndian(container.AsSpan(12)) == 600_000,
                "Неверное число итераций.");
            VaultFormat.ValidateHeader(container, container.Length);
        });
        RunCheck("Расшифровка созданного .pvlt: точные имя и байты", () =>
        {
            using var restored = VaultFormat.Decrypt(GetContainer(container), Password);
            CheckRestored(restored, FileName, sample);
        });
        RunCheck("Fixture v1: наследие.txt", () => CheckFixture("V1", "Пароль совместимости 2026",
            "наследие.txt", "PocketVault v1 — совместимость формата.\n"));
        RunCheck("Fixture v1.1.0: v1.1.0-example.txt", () => CheckFixture("V110", "Public fixture password 1.1.0",
            "v1.1.0-example.txt", "PocketVault 1.1.0 compatibility fixture.\n"));
        RunCheck("Неверный пароль отклоняется", () => ExpectVaultRejection(GetContainer(container), "wrong password"));
        RunCheck("Повреждённые шифротекст и тег отклоняются", () =>
        {
            foreach (int offset in new[] { VaultFormat.HeaderSize, GetContainer(container).Length - 1 })
            {
                byte[] changed = (byte[])GetContainer(container).Clone();
                changed[offset] ^= 1;
                ExpectVaultRejection(changed, Password);
            }
        });
        RunCheck("Изменённый заголовок AAD отклоняется", () =>
        {
            foreach (int offset in new[] { 16, 32 })
            {
                byte[] changed = (byte[])GetContainer(container).Clone();
                changed[offset] ^= 1;
                ExpectVaultRejection(changed, Password);
            }
        });
        RunCheck("Пустой файл: точные имя и нулевая длина", () =>
        {
            using var restored = VaultFormat.Decrypt(VaultFormat.Encrypt("empty.txt", [], Password), Password);
            CheckRestored(restored, "empty.txt", []);
        });

        return new(runId, DateTimeOffset.UtcNow,
            $"Android {(global::Android.OS.Build.VERSION.Release)}; {RuntimeInformation.FrameworkDescription}; {RuntimeInformation.ProcessArchitecture}",
            (int)global::Android.OS.Build.VERSION.SdkInt, checks.All(check => check.Success), checks);
    }

    private static void CheckAesGcm()
    {
        Require(AesGcm.IsSupported, "AesGcm.IsSupported == false");
        using var aes = new AesGcm(new byte[32], 16);
        byte[] ciphertext = new byte[16], tag = new byte[16], plaintext = new byte[16];
        // NIST GCM, 256-bit zero key, 96-bit zero IV, 128-bit zero plaintext.
        aes.Encrypt(new byte[12], new byte[16], ciphertext, tag);
        Require(Convert.ToHexString(ciphertext) == "CEA7403D4D606B6E074EC5D3BAF39D18", "AES ciphertext mismatch.");
        Require(Convert.ToHexString(tag) == "D0D1C8A799996BF0265B98B5D48AB919", "AES tag mismatch.");
        aes.Decrypt(new byte[12], ciphertext, tag, plaintext);
        Require(plaintext.AsSpan().SequenceEqual(new byte[16]), "AES plaintext mismatch.");
        byte[] aad = "PocketVault smoke AAD"u8.ToArray();
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
        aad[0] ^= 1;
        try { aes.Decrypt(nonce, ciphertext, tag, plaintext, aad); }
        catch (AuthenticationTagMismatchException) { return; }
        throw new InvalidOperationException("AES принял изменённый AAD.");
    }

    private static void CheckPbkdf2()
    {
        // RFC 7914 section 11: PBKDF2-HMAC-SHA256, c=1, dkLen=64.
        byte[] key = Rfc2898DeriveBytes.Pbkdf2("passwd", "salt"u8, 1, HashAlgorithmName.SHA256, 64);
        try
        {
            Require(Convert.ToHexString(key) ==
                "55AC046E56E3089FEC1691C22544B605F94185216DDE0465E68B9D57C20DACBC4" +
                "9CA9CCCF179B645991664B39D77EF317C71B845B1E30BD509112041D3A19783", "PBKDF2 vector mismatch.");
            // The .pvlt roundtrip and both fixed fixtures separately exercise c=600,000.
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private static void CheckZip(byte[] sample)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, true, Encoding.UTF8))
        using (var entry = archive.CreateEntry(FileName, CompressionLevel.Optimal).Open())
            entry.Write(sample);
        buffer.Position = 0;
        using var zip = new ZipArchive(buffer, ZipArchiveMode.Read, true, Encoding.UTF8);
        Require(zip.Entries.Count == 1 && zip.Entries[0].FullName == FileName, "ZIP name mismatch.");
        using var source = zip.Entries[0].Open();
        using var restored = new MemoryStream();
        source.CopyTo(restored);
        Require(restored.ToArray().AsSpan().SequenceEqual(sample), "ZIP bytes mismatch.");
    }

    private static void CheckFixture(string resource, string password, string name, string contents)
    {
        using var stream = typeof(CryptoSmokeTest).Assembly.GetManifestResourceStream($"PocketVault.Android.Fixtures.{resource}")
            ?? throw new InvalidOperationException($"Fixture {resource} не встроен в приложение.");
        using var reader = new StreamReader(stream);
        using var restored = VaultFormat.Decrypt(Convert.FromBase64String(reader.ReadToEnd()), password);
        CheckRestored(restored, name, Encoding.UTF8.GetBytes(contents));
    }

    private static byte[] GetContainer(byte[]? container) => container
        ?? throw new InvalidOperationException("Создание контейнера не прошло.");

    private static void CheckRestored(RestoredFile restored, string name, ReadOnlySpan<byte> contents)
    {
        Require(restored.Name == name, "Имя восстановлено неточно.");
        Require(restored.Data.AsSpan().SequenceEqual(contents), "Содержимое восстановлено неточно.");
    }

    private static void ExpectVaultRejection(byte[] container, string password)
    {
        try { using var unexpected = VaultFormat.Decrypt(container, password); }
        catch (VaultException ex) when (ex.InnerException is AuthenticationTagMismatchException) { return; }
        throw new InvalidOperationException("Ожидался отказ аутентификации контейнера.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
