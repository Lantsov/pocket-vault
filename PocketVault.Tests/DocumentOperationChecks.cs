using System.IO;
using System.Security.Cryptography;
using System.Text;
using PocketVault.Android.Services;
using PocketVault.Core;

namespace PocketVault.Tests;

// The same synthetic checks execute on Windows and, in Debug, on the Android runtime.
internal sealed record DocumentCheck(string Name, bool Success, string Detail);

internal static class DocumentOperationChecks
{
    private const string Password = "Public synthetic stage 2 password";

    internal static async Task<IReadOnlyList<DocumentCheck>> RunAsync()
    {
        var checks = new List<DocumentCheck>();
        async Task Check(string name, Func<Task> action)
        {
            try { await action(); checks.Add(new(name, true, "OK")); }
            catch (Exception ex) { checks.Add(new(name, false, ex.GetType().Name + ": " + ex.Message)); }
        }

        foreach (var (name, data) in new (string, byte[])[]
        {
            ("пустой.txt", []),
            ("Кириллица 日本語 🗝.txt", Encoding.UTF8.GetBytes("Привет, Android!\n")),
            ("report #1 (a+b) & 'test'.bin", [0, 1, 127, 128, 255]),
            ("archive.zip", RandomNumberGenerator.GetBytes(8192)),
            ("image.png", [137, 80, 78, 71, 0, 255])
        })
        {
            await Check("Roundtrip: " + name, async () =>
            {
                var fake = new FakeDocuments(data) { Document = new("content://test/source", name, null) };
                Require(await new DocumentOperations(fake).RunAsync(fake.Document, Password, Password));
                Require(fake.SuggestedName!.StartsWith("vault-", StringComparison.Ordinal) && fake.SuggestedName.EndsWith(".pvlt", StringComparison.Ordinal));
                Require(!fake.SuggestedName.Contains(name, StringComparison.Ordinal));
                using var windows = VaultFormat.Decrypt(fake.Written!, Password);
                Require(windows.Name == name && windows.Data.AsSpan().SequenceEqual(data));
                byte[] container = VaultFormat.Encrypt(name, data, Password);
                var decrypt = new FakeDocuments(container);
                Require(await new DocumentOperations(decrypt).RunAsync(decrypt.Document, Password, ""));
                Require(decrypt.SuggestedName == name && decrypt.Written!.AsSpan().SequenceEqual(data));
                Require(fake.CapturedWriteBuffer!.All(b => b == 0) && decrypt.CapturedWriteBuffer!.All(b => b == 0));
            });
        }

        byte[] valid = VaultFormat.Encrypt("restored.bin", [1, 2, 255], Password);
        foreach (string failure in new[] { "wrong password", "corrupt", "truncated", "header", "trailing" })
            await Check("No output: " + failure, async () =>
            {
                byte[] bytes = (byte[])valid.Clone();
                string password = Password;
                if (failure == "wrong password") password = "wrong";
                if (failure == "corrupt") bytes[^1] ^= 1;
                if (failure == "truncated") bytes = bytes[..^1];
                if (failure == "header") bytes[0] ^= 1;
                if (failure == "trailing") bytes = [.. bytes, 0];
                var fake = new FakeDocuments(bytes);
                await Reject<VaultException>(() => new DocumentOperations(fake).RunAsync(fake.Document, password, ""));
                Require(fake.CreateCalls == 0 && fake.DeleteCalls == 0);
            });

        await Check("Password validation before read/create", async () =>
        {
            var fake = new FakeDocuments([]) { Document = new("content://test/source", "input.bin", 0) };
            var operations = new DocumentOperations(fake);
            await Reject<VaultException>(() => operations.RunAsync(fake.Document, "", ""));
            await Reject<VaultException>(() => operations.RunAsync(fake.Document, Password, "mismatch"));
            Require(fake.ReadCalls == 0 && fake.CreateCalls == 0);
        });
        await Check("Unsafe name rejected before read", async () =>
        {
            foreach (string name in new[] { "../escape", "CON.txt", "bad:name", "trailing.", "", "dir/file" })
            {
                var fake = new FakeDocuments([]) { Document = new("content://test/source", name, null) };
                await Reject<VaultException>(() => new DocumentOperations(fake).RunAsync(fake.Document, Password, Password));
                Require(fake.ReadCalls == 0 && fake.CreateCalls == 0);
            }
        });
        await Check("Known oversize rejected before read", async () =>
        {
            var fake = new FakeDocuments([]) { Document = new("content://test/source", "large.bin", DocumentOperations.MaxFileSize + 1L) };
            await Reject<VaultException>(() => new DocumentOperations(fake).RunAsync(fake.Document, Password, Password));
            Require(fake.ReadCalls == 0 && fake.CreateCalls == 0);
        });
        foreach (long? reportedSize in new long?[] { null, 1 })
            await Check("Unknown/false size: hard read limit " + (reportedSize?.ToString() ?? "null"), async () =>
            {
                var fake = new FakeDocuments([]) { Document = new("content://test/source", "large.bin", reportedSize), GeneratedLength = DocumentOperations.MaxFileSize + 1L };
                await Reject<VaultException>(() => new DocumentOperations(fake).RunAsync(fake.Document, Password, Password));
                Require(fake.CreateCalls == 0 && fake.BytesRead == DocumentOperations.MaxFileSize + 1L);
            });
        await Check("Container read limit with missing metadata", async () =>
        {
            var fake = new FakeDocuments([]) { GeneratedLength = DocumentOperations.MaxContainerSize + 1L };
            await Reject<VaultException>(() => new DocumentOperations(fake).RunAsync(fake.Document, Password, ""));
            Require(fake.CreateCalls == 0 && fake.BytesRead == DocumentOperations.MaxContainerSize + 1L);
        });
        await Check("32 MiB accepted; compressed 32 MiB + 1 rejected before restored allocation; Windows default retained", async () =>
        {
            byte[] large = new byte[DocumentOperations.MaxFileSize + 1];
            byte[] over = VaultFormat.Encrypt("large.bin", large, Password);
            var fake = new FakeDocuments(over);
            await Reject<VaultException>(() => new DocumentOperations(fake).RunAsync(fake.Document, Password, ""));
            Require(fake.CreateCalls == 0);
            using (var windows = VaultFormat.Decrypt(over, Password)) Require(windows.Data.Length == large.Length);
            var boundary = new FakeDocuments([]) { Document = new("content://test/source", "boundary.bin", null), GeneratedLength = DocumentOperations.MaxFileSize };
            Require(await new DocumentOperations(boundary).RunAsync(boundary.Document, Password, Password));
            using var exact = VaultFormat.Decrypt(boundary.Written!, Password, DocumentOperations.MaxFileSize);
            Require(exact.Data.Length == DocumentOperations.MaxFileSize && exact.Data.All(b => b == 0));
            CryptographicOperations.ZeroMemory(large);
        });
        await Check("Core custom limits (zero, exact, invalid)", () =>
        {
            using var empty = VaultFormat.Decrypt(VaultFormat.Encrypt("empty", [], Password), Password, 0);
            Require(empty.Data.Length == 0);
            using var exact = VaultFormat.Decrypt(valid, Password, 3);
            Require(exact.Data.AsSpan().SequenceEqual(new byte[] { 1, 2, 255 }));
            try { using var unexpected = VaultFormat.Decrypt(valid, Password, -1); throw new InvalidOperationException(); }
            catch (ArgumentOutOfRangeException) { }
            try { using var unexpected = VaultFormat.Decrypt(valid, Password, VaultFormat.MaxFileSize + 1); throw new InvalidOperationException(); }
            catch (ArgumentOutOfRangeException) { }
            return Task.CompletedTask;
        });
        await Check("Cancel picker", async () =>
        {
            var fake = new FakeDocuments([]);
            Require(await fake.PickAsync() is null && fake.ReadCalls == 0 && fake.CreateCalls == 0);
        });
        await Check("Cancel save clears plaintext", async () =>
        {
            var fake = new FakeDocuments(valid) { CancelSave = true };
            Require(!await new DocumentOperations(fake).RunAsync(fake.Document, Password, ""));
            Require(fake.CapturedWriteBuffer is null && fake.DeleteCalls == 0);
        });
        await Check("Revoked read permission", async () =>
        {
            var fake = new FakeDocuments(valid) { ReadDenied = true };
            await Reject<UnauthorizedAccessException>(() => new DocumentOperations(fake).RunAsync(fake.Document, Password, ""));
            Require(fake.CreateCalls == 0 && fake.DeleteCalls == 0);
        });
        await Check("Out of memory before output; operation gate recovers", async () =>
        {
            var fake = new FakeDocuments(valid) { ReadOutOfMemory = true };
            var operations = new DocumentOperations(fake);
            await Reject<OutOfMemoryException>(() => operations.RunAsync(fake.Document, Password, ""));
            Require(fake.CreateCalls == 0 && fake.DeleteCalls == 0);
            fake.ReadOutOfMemory = false;
            Require(await operations.RunAsync(fake.Document, Password, ""));
        });
        foreach (string failure in new[] { "open", "write", "flush", "close" })
            foreach (bool deleteFails in new[] { false, true })
                await Check($"Provider {failure} failure; delete {(deleteFails ? "denied" : "succeeds")}", async () =>
                {
                    var fake = new FakeDocuments(valid) { WriteFailure = failure, DeleteFails = deleteFails };
                    try { await new DocumentOperations(fake).RunAsync(fake.Document, Password, ""); throw new InvalidOperationException("Expected write error"); }
                    catch (DocumentWriteException ex)
                    {
                        Require(ex.Message.Contains(deleteFails ? "вручную" : "документ удалён", StringComparison.Ordinal));
                    }
                    Require(fake.CreateCalls == 1 && fake.DeleteCalls == 1);
                    Require(fake.DeletedId == "content://test/new");
                });
        await Check("Provider must not return original URI", async () =>
        {
            var fake = new FakeDocuments(valid) { ReturnSource = true };
            await Reject<VaultException>(() => new DocumentOperations(fake).RunAsync(fake.Document, Password, ""));
            Require(fake.DeleteCalls == 0 && fake.CapturedWriteBuffer is null);
        });
        await Check("Duplicate operation blocked; gate recovers after cancellation", async () =>
        {
            var fake = new FakeDocuments(valid) { PendingCreate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
            var operations = new DocumentOperations(fake);
            Task<bool> first = operations.RunAsync(fake.Document, Password, "");
            await fake.CreateEntered.Task;
            await Reject<VaultException>(() => operations.RunAsync(fake.Document, Password, ""));
            fake.PendingCreate.SetResult(null);
            Require(!await first);
            fake.PendingCreate = null;
            Require(await operations.RunAsync(fake.Document, Password, ""));
        });
        return checks;
    }

    private static void Require(bool value) { if (!value) throw new InvalidOperationException("Document check failed."); }
    private static async Task Reject<T>(Func<Task<bool>> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }

    private sealed class FakeDocuments(byte[] input) : IDocumentAccess
    {
        internal VaultDocument Document { get; init; } = new("content://test/source", "input.PVLT", null);
        internal string? SuggestedName;
        internal byte[]? Written, CapturedWriteBuffer;
        internal int CreateCalls, ReadCalls, DeleteCalls;
        internal bool CancelSave, ReadDenied, DeleteFails, ReturnSource;
        internal bool ReadOutOfMemory;
        internal string? WriteFailure, DeletedId;
        internal long? GeneratedLength;
        internal long BytesRead;
        internal TaskCompletionSource<VaultDocument?>? PendingCreate;
        internal TaskCompletionSource CreateEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<VaultDocument?> PickAsync() => Task.FromResult<VaultDocument?>(null);
        public Task<VaultDocument?> CreateAsync(string name)
        {
            CreateCalls++;
            SuggestedName = name;
            CreateEntered.TrySetResult();
            return PendingCreate?.Task ?? Task.FromResult(CancelSave ? null : ReturnSource ? Document : new VaultDocument("content://test/new", name, null));
        }
        public Stream OpenRead(VaultDocument document)
        {
            ReadCalls++;
            if (ReadDenied) throw new UnauthorizedAccessException();
            if (ReadOutOfMemory) throw new OutOfMemoryException();
            return GeneratedLength is long length ? new GeneratedStream(this, length) : new MemoryStream(input, false);
        }
        public Stream OpenWrite(VaultDocument document)
        {
            if (WriteFailure == "open") throw new UnauthorizedAccessException();
            return new OutputStream(this);
        }
        public bool TryDeleteCreated(VaultDocument document)
        {
            DeleteCalls++;
            DeletedId = document.Id;
            if (DeleteFails) throw new UnauthorizedAccessException();
            return true;
        }
        private sealed class OutputStream(FakeDocuments owner) : MemoryStream
        {
            public override void Write(byte[] buffer, int offset, int count)
            {
                owner.CapturedWriteBuffer = buffer;
                if (owner.WriteFailure == "write") { base.Write(buffer, offset, Math.Min(count, 1)); throw new IOException(); }
                base.Write(buffer, offset, count);
                owner.Written = ToArray();
            }
            public override void Flush() { if (owner.WriteFailure == "flush") throw new IOException(); base.Flush(); }
            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (owner.WriteFailure == "close") throw new IOException();
            }
        }
        private sealed class GeneratedStream(FakeDocuments owner, long length) : Stream
        {
            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = (int)Math.Min(count, length - owner.BytesRead);
                Array.Clear(buffer, offset, read);
                owner.BytesRead += read;
                return read;
            }
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
