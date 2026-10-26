using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using PocketVault.App;
using PocketVault.Core;

namespace PocketVault.Tests;

internal static class Program
{
    private const string Password = "Тестовый пароль 🗝 47";
    private static readonly byte[] Sample = RandomNumberGenerator.GetBytes(4096);
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "PocketVault-tests-" + Guid.NewGuid().ToString("N"));
    private static int passed;
    private static int failed;

    [STAThread]
    private static int Main()
    {
        Directory.CreateDirectory(Root);
        try
        {
            byte[] valid = VaultFormat.Encrypt("Личный документ 🗝.txt", Sample, Password);
            Run("Шифрование и побайтовое восстановление", () =>
            {
                using var file = VaultFormat.Decrypt(valid, Password);
                Check(file.Name == "Личный документ 🗝.txt" && file.Data.SequenceEqual(Sample));
            });
            Run("Неверный пароль", () => Throws<VaultException>(() => VaultFormat.Decrypt(valid, "другой пароль")));
            Run("Повреждение шифротекста", () => RejectMutation(valid, VaultFormat.HeaderSize + 10));
            Run("Повреждение тега", () => RejectMutation(valid, valid.Length - 1));
            Run("Аутентификация соли заголовка", () => RejectMutation(valid, 16));
            Run("Аутентификация nonce заголовка", () => RejectMutation(valid, 32));
            Run("Повреждение сигнатуры", () => RejectMutation(valid, 0));
            Run("Неподдерживаемая версия", () => RejectMutation(valid, 8));
            Run("Повреждение KDF, шифра, флагов и итераций", () => { foreach (int offset in new[] { 9, 10, 11, 12 }) RejectMutation(valid, offset); });
            Run("Повреждение длины заголовка", () => RejectMutation(valid, 44));
            Run("Обрезанные контейнеры", () =>
            {
                foreach (int length in new[] { 0, 7, 51, 52, valid.Length - 1 })
                    Throws<VaultException>(() => VaultFormat.Decrypt(valid.AsSpan(0, length), Password));
            });
            Run("Лишние байты в контейнере", () => Throws<VaultException>(() => VaultFormat.Decrypt([.. valid, 0], Password)));
            Run("Пустой исходный файл", () =>
            {
                using var file = VaultFormat.Decrypt(VaultFormat.Encrypt("пустой.txt", [], Password), Password);
                Check(file.Data.Length == 0 && file.Name == "пустой.txt");
            });
            Run("Независимые соль и nonce при одинаковом пароле", () =>
            {
                byte[] other = VaultFormat.Encrypt("Личный документ 🗝.txt", Sample, Password);
                Check(!other.SequenceEqual(valid));
                Check(!other.AsSpan(16, 16).SequenceEqual(valid.AsSpan(16, 16)));
                Check(!other.AsSpan(32, 12).SequenceEqual(valid.AsSpan(32, 12)));
                byte[] different = VaultFormat.Encrypt("другой.bin", [1, 2, 3], Password);
                Check(!different.SequenceEqual(valid));
            });
            Run("Отсутствие имени в открытом виде", () =>
            {
                Check(valid.AsSpan().IndexOf(Encoding.UTF8.GetBytes("Личный документ 🗝.txt")) == -1);
                Check(valid.AsSpan().IndexOf(Encoding.Unicode.GetBytes("Личный документ 🗝.txt")) == -1);
            });
            Run("Защита от Zip Slip и имён устройств", () =>
            {
                foreach (string name in new[] { "../escape.txt", "..\\escape.txt", "C:\\escape.txt", "/escape", "dir/file", "file:stream", "CON.txt", "COM1", "LPT².txt", "bad.", "bad ", "..", "" })
                    Throws<VaultException>(() => VaultFormat.ValidateFileName(name));
            });
            Run("Отказ от аутентифицированного ZIP с путём", () => Throws<VaultException>(() => VaultFormat.Decrypt(SealZip("../escape.txt"), Password)));
            Run("Отказ от ZIP с несколькими записями", () => Throws<VaultException>(() => VaultFormat.Decrypt(SealZip("one.txt", "two.txt"), Password)));
            Run("Отказ от ZIP без записей", () => Throws<VaultException>(() => VaultFormat.Decrypt(SealZip(), Password)));
            Run("Слишком большой распакованный файл отклоняется", () =>
            {
                using var buffer = new MemoryStream();
                using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
                using (var entry = zip.CreateEntry("oversize.bin").Open())
                {
                    byte[] block = new byte[1024 * 1024];
                    for (int i = 0; i < 129; i++) entry.Write(block);
                }
                Throws<VaultException>(() => VaultFormat.Decrypt(Seal(buffer.ToArray()), Password));
            });
            Run("Отказ от некорректного ZIP после аутентификации", () => Throws<VaultException>(() => VaultFormat.Decrypt(Seal(new byte[40]), Password)));
            Run("Пустой пароль запрещён", () => Throws<VaultException>(() => VaultFormat.Encrypt("a", Sample, "")));
            Run("Завышенная длина контейнера отвергается до KDF", () =>
            {
                byte[] huge = (byte[])valid.Clone();
                BinaryPrimitives.WriteInt64LittleEndian(huge.AsSpan(44), long.MaxValue);
                Throws<VaultException>(() => VaultFormat.Decrypt(huge, Password));
            });
            Run("Файловый цикл с Unicode-именем", DiskRoundtrip);
            Run("Существующий контейнер не перезаписывается", () =>
            {
                string source = Write("source.bin", Sample), target = Write("exists.pvlt", [9, 8, 7]);
                Throws<VaultException>(() => VaultFiles.Encrypt(source, target, Password));
                Check(File.ReadAllBytes(target).SequenceEqual(new byte[] { 9, 8, 7 }));
            });
            Run("Существующий восстановленный файл не перезаписывается", () =>
            {
                string container = Write("collision.pvlt", valid);
                string existing = Write("Личный документ 🗝.txt", [5]);
                Throws<VaultException>(() => VaultFiles.Decrypt(container, Root, Password));
                Check(File.ReadAllBytes(existing).SequenceEqual(new byte[] { 5 }));
                Check(!Directory.EnumerateFiles(Root, ".pvlt-*.tmp").Any());
            });
            Run("Нет частичного результата при неверном пароле", () =>
            {
                string folder = Path.Combine(Root, "failed"); Directory.CreateDirectory(folder);
                Throws<VaultException>(() => VaultFiles.Decrypt(Write("bad-password.pvlt", valid), folder, "bad"));
                Check(!Directory.EnumerateFileSystemEntries(folder).Any());
            });
            Run("Отсутствующий файл", () => Throws<FileNotFoundException>(() => VaultFiles.Decrypt(Path.Combine(Root, "missing.pvlt"), Root, Password)));
            Run("Ограничение размера до чтения в память", () =>
            {
                string source = Path.Combine(Root, "large.bin");
                using (var stream = File.Create(source)) stream.SetLength(VaultFormat.MaxFileSize + 1L);
                Throws<VaultException>(() => VaultFiles.Encrypt(source, Path.Combine(Root, "large.pvlt"), Password));
                Check(!File.Exists(Path.Combine(Root, "large.pvlt")));
            });
            Run("Удаление исходника после проверки", () =>
            {
                string source = Write("delete-me.bin", Sample), target = Path.Combine(Root, "delete-verified.pvlt");
                VaultFiles.Encrypt(source, target, Password, true);
                Check(!File.Exists(source));
                using var restored = VaultFormat.Decrypt(File.ReadAllBytes(target), Password);
                Check(restored.Data.SequenceEqual(Sample));
            });
            Run("Исходник сохранён при ошибке записи с удалением", () =>
            {
                string source = Write("keep-me.bin", Sample), target = Write("do-not-overwrite.pvlt", [1]);
                Throws<VaultException>(() => VaultFiles.Encrypt(source, target, Password, true));
                Check(File.ReadAllBytes(source).SequenceEqual(Sample));
            });
            Run("Одинаковые пути запрещены", () =>
            {
                string source = Write("same.bin", Sample);
                Throws<VaultException>(() => VaultFiles.Encrypt(source, source, Password));
                Check(File.ReadAllBytes(source).SequenceEqual(Sample));
            });
            Run("Безопасное случайное имя контейнера", () =>
            {
                string a = VaultFiles.SuggestContainerName(), b = VaultFiles.SuggestContainerName();
                Check(a.StartsWith("vault-", StringComparison.Ordinal) && a.EndsWith(".pvlt", StringComparison.Ordinal) && a != b);
            });
            Run("GUI: иконка, запуск с .pvlt и размещение элементов", GuiSmoke);
        }
        finally { Directory.Delete(Root, true); }
        Console.WriteLine($"\nПройдено: {passed}; ошибок: {failed}. Временные файлы удалены.");
        return failed == 0 ? 0 : 1;
    }

    private static void DiskRoundtrip()
    {
        string dir = Path.Combine(Root, "restored"); Directory.CreateDirectory(dir);
        string source = Write("секретный отчёт 日本語.txt", Sample);
        string container = Path.Combine(Root, VaultFiles.SuggestContainerName());
        VaultFiles.Encrypt(source, container, Password);
        string restored = VaultFiles.Decrypt(container, dir, Password);
        Check(Path.GetFileName(restored) == Path.GetFileName(source));
        Check(File.ReadAllBytes(restored).SequenceEqual(Sample));
        Check(File.Exists(source));
        Check(!Directory.EnumerateFiles(Root, "*.zip", SearchOption.AllDirectories).Any());
        Check(!Directory.EnumerateFiles(Root, ".pvlt-*.tmp", SearchOption.AllDirectories).Any());
    }

    private static void GuiSmoke()
    {
        ApplicationConfiguration.Initialize();
        using var form = new MainForm(Path.Combine(Root, "sample.pvlt"));
        form.Show(); form.PerformLayout(); Application.DoEvents();
        var controls = AllControls(form).ToArray();
        Check(form.Icon is not null);
        Check(controls.OfType<RadioButton>().Any(r => r.Text == "Расшифровать" && r.Checked));
        Check(controls.OfType<TextBox>().Any(t => t.Text.EndsWith("sample.pvlt", StringComparison.Ordinal)));
        var encryptButton = controls.OfType<RadioButton>().Single(r => r.Text == "Зашифровать");
        encryptButton.Checked = true;
        form.PerformLayout(); Application.DoEvents();
        Check(controls.OfType<CheckBox>().Single(c => c.Text.StartsWith("Удалить", StringComparison.Ordinal)).Checked == false);
        foreach (Control control in controls.Where(c => c.Visible && c.Parent is TableLayoutPanel && c.Dock != DockStyle.Fill))
            Check(control.Bottom <= control.Parent!.ClientSize.Height + 1);
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
        string? screenshot = Environment.GetEnvironmentVariable("PVLT_SCREENSHOT");
        if (!string.IsNullOrEmpty(screenshot)) bitmap.Save(screenshot);
        form.Close();
    }

    private static IEnumerable<Control> AllControls(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            yield return control;
            foreach (var child in AllControls(control)) yield return child;
        }
    }

    private static byte[] SealZip(params string[] names)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
            foreach (string name in names)
                using (var entry = zip.CreateEntry(name).Open()) entry.Write(Sample);
        return Seal(buffer.ToArray());
    }

    // Независимый тестовый создатель аутентифицированных, но опасных архивов.
    private static byte[] Seal(byte[] zip)
    {
        byte[] result = new byte[VaultFormat.HeaderSize + zip.Length + VaultFormat.TagSize];
        Span<byte> header = result.AsSpan(0, VaultFormat.HeaderSize);
        "PVLT\r\n\x1a\n"u8.CopyTo(header);
        header[8] = header[9] = header[10] = 1;
        BinaryPrimitives.WriteInt32LittleEndian(header[12..], VaultFormat.Iterations);
        RandomNumberGenerator.Fill(header.Slice(16, 28));
        BinaryPrimitives.WriteInt64LittleEndian(header[44..], zip.Length);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(Password, header.Slice(16, 16), VaultFormat.Iterations, HashAlgorithmName.SHA256, 32);
        using var aes = new AesGcm(key, VaultFormat.TagSize);
        aes.Encrypt(header.Slice(32, 12), zip, result.AsSpan(VaultFormat.HeaderSize, zip.Length), result.AsSpan(VaultFormat.HeaderSize + zip.Length), header);
        CryptographicOperations.ZeroMemory(key);
        return result;
    }

    private static void RejectMutation(byte[] original, int offset)
    {
        byte[] copy = (byte[])original.Clone(); copy[offset] ^= 0x40;
        Throws<VaultException>(() => VaultFormat.Decrypt(copy, Password));
    }
    private static string Write(string name, byte[] data) { string path = Path.Combine(Root, name); File.WriteAllBytes(path, data); return path; }
    private static void Check(bool condition) { if (!condition) throw new InvalidOperationException("Проверка не пройдена."); }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Ожидалось исключение {typeof(T).Name}.");
    }
    private static void Run(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("[OK] " + name); }
        catch (Exception ex) { failed++; Console.WriteLine($"[ОШИБКА] {name}: {ex}"); }
    }
}
