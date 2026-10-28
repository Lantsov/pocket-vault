using System.IO;
using System.Security.Cryptography;
using PocketVault.Core;

namespace PocketVault.Android.Services;

internal sealed record VaultDocument(string Id, string Name, long? Size)
{
    internal bool IsContainer => Name.EndsWith(".pvlt", StringComparison.OrdinalIgnoreCase);
}

// Only Open/Create dialogs can grant these document references. No filesystem paths.
internal interface IDocumentAccess
{
    Task<VaultDocument?> PickAsync();
    Task<VaultDocument?> CreateAsync(string suggestedName);
    Stream OpenRead(VaultDocument document);
    Stream OpenWrite(VaultDocument document);
    bool TryDeleteCreated(VaultDocument document);
}

internal sealed class DocumentWriteException(bool removed) : Exception(removed
    ? "Запись не завершена. Созданный документ удалён; исходный файл сохранён."
    : "Запись не завершена. Не удалось удалить созданный документ: в выбранном месте мог остаться пустой или частично записанный файл. Проверьте и удалите его вручную. Исходный файл сохранён.");

internal sealed class DocumentOperations(IDocumentAccess documents)
{
    internal const int MaxFileSize = 32 * 1024 * 1024;
    internal const int MaxContainerSize = MaxFileSize + 1024 * 1024 + VaultFormat.HeaderSize + VaultFormat.TagSize;
    private int running;

    internal static void ValidateDocument(VaultDocument document)
    {
        VaultFormat.ValidateFileName(document.Name);
        long limit = document.IsContainer ? MaxContainerSize : MaxFileSize;
        if (document.Size > limit) throw SizeError(document.IsContainer);
    }

    // Returns false only if the user cancels Save As. Nothing is created before authentication.
    internal async Task<bool> RunAsync(VaultDocument document, string password, string confirmation)
    {
        if (Interlocked.CompareExchange(ref running, 1, 0) != 0)
            throw new VaultException("Операция уже выполняется.");
        byte[]? encrypted = null;
        RestoredFile? restored = null;
        try
        {
            ValidateDocument(document);
            if (string.IsNullOrEmpty(password)) throw new VaultException("Введите непустой пароль.");
            if (!document.IsContainer && password != confirmation) throw new VaultException("Пароли не совпадают.");
            await Task.Run(() =>
            {
                using var input = documents.OpenRead(document);
                using var data = ReadBounded(input, document.IsContainer);
                if (document.IsContainer)
                    restored = VaultFormat.Decrypt(data.GetBuffer().AsSpan(0, (int)data.Length), password, MaxFileSize);
                else
                    encrypted = VaultFormat.Encrypt(document.Name, data.GetBuffer().AsSpan(0, (int)data.Length), password);
            });
            string name = restored?.Name ?? VaultFiles.SuggestContainerName();
            VaultDocument? destination = await documents.CreateAsync(name);
            if (destination is null) return false;
            // A misbehaving provider must never cause source overwrite or source cleanup.
            if (destination.Id == document.Id)
                throw new VaultException("Провайдер вернул исходный документ вместо нового. Сохранение отменено.");
            await Task.Run(() => WriteCreated(destination, restored?.Data ?? encrypted!));
            return true;
        }
        finally
        {
            if (encrypted is not null) CryptographicOperations.ZeroMemory(encrypted);
            restored?.Dispose();
            Volatile.Write(ref running, 0);
        }
    }

    private void WriteCreated(VaultDocument destination, byte[] data)
    {
        try
        {
            // Dispose is inside try: a provider may fail only on flush/close.
            using var output = documents.OpenWrite(destination);
            output.Write(data, 0, data.Length);
            output.Flush();
        }
        catch
        {
            bool removed;
            try { removed = documents.TryDeleteCreated(destination); }
            catch { removed = false; }
            throw new DocumentWriteException(removed);
        }
    }

    private static ClearedMemoryStream ReadBounded(Stream input, bool container)
    {
        int limit = container ? MaxContainerSize : MaxFileSize;
        var result = new ClearedMemoryStream();
        byte[] block = new byte[64 * 1024];
        try
        {
            while (true)
            {
                // One extra byte detects excess even when metadata is missing or false.
                int count = input.Read(block, 0, (int)Math.Min(block.Length, limit - result.Length + 1));
                if (count == 0) return result;
                if (result.Length + count > limit) throw SizeError(container);
                if (result.Capacity < result.Length + count)
                {
                    int capacity = (int)Math.Min(limit, Math.Max(result.Length + count, Math.Max(64 * 1024L, result.Capacity * 2L)));
                    // MemoryStream growth otherwise abandons an uncleared plaintext array.
                    byte[] old = result.GetBuffer();
                    result.Capacity = capacity;
                    CryptographicOperations.ZeroMemory(old);
                }
                result.Write(block, 0, count);
            }
        }
        catch { result.Dispose(); throw; }
        finally { CryptographicOperations.ZeroMemory(block); }
    }

    private static VaultException SizeError(bool container) => new(container
        ? "Контейнер превышает лимит Android MVP (32 МиБ данных и служебные данные ZIP)."
        : "Размер исходного файла превышает лимит Android MVP — 32 МиБ.");

    private sealed class ClearedMemoryStream : MemoryStream
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing) CryptographicOperations.ZeroMemory(GetBuffer());
            base.Dispose(disposing);
        }
    }
}
