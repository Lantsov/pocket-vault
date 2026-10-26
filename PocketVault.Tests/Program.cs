using System.Buffers.Binary;
using System.IO.Compression;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
    private static int Main(string[] args)
    {
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(application.Dispatcher));
        if (args is ["--screenshots", string output, string samples])
        {
            CaptureDocumentation(output, samples);
            application.Shutdown();
            return 0;
        }
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
            Run("WPF: иконка, открытие .pvlt, режимы и очистка пароля", GuiSmoke);
            Run("WPF: перетаскивание одного файла, отказ для папок и нескольких файлов", GuiDrop);
            Run("WPF: проверка пароля, подтверждения и фокуса", GuiValidation);
            Run("WPF: шифрование, занятость, защита закрытия и восстановление", GuiOperations);
            Run("WPF: отмена диалога и подтверждение удаления", GuiDeletion);
            Run("WPF: ошибки и восстановление состояния", GuiErrors);
            Run("WPF: восстановление контейнера версии до миграции", GuiLegacyContainer);
            Run("WPF: настройки и безопасная отмена предупреждения", GuiDialogs);
            Run("WPF: размещение и снимки при 100, 125, 150 и 200%", GuiLayout);
        }
        finally { application.Shutdown(); Directory.Delete(Root, true); }
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
        string container = Write("запуск из Проводника.PVLT", [0]);
        var window = OpenWindow(container);
        try
        {
            Check(window.Icon is not null && !window.IsEncryptMode && window.SelectedPath == container);
            Check(window.ConfirmationPanel.Visibility == Visibility.Collapsed && window.DeleteOriginal.Visibility == Visibility.Collapsed);
            Check(!window.EncryptButton.IsEnabled && !window.DecryptButton.IsEnabled);
            window.Password.Password = Password;
            Check(window.DecryptButton.IsEnabled);
            Click(window.ShowPasswordButton);
            Check(window.VisiblePassword.Text == Password && window.Password.Visibility == Visibility.Collapsed);
            window.VisiblePassword.Text = "изменённый пароль";
            Check(window.Password.Password == "изменённый пароль");
            Click(window.ShowPasswordButton);
            Check(window.VisiblePassword.Text.Length == 0 && window.Password.Password == "изменённый пароль");
            window.SelectFile(Write("обычный файл.txt", Sample));
            Check(window.IsEncryptMode && window.ConfirmationPanel.Visibility == Visibility.Visible);
            Check(window.Password.Password.Length == 0 && window.Confirmation.Password.Length == 0);
            window.DeleteOriginal.IsChecked = true;
            window.SelectFile(container);
            Check(window.DeleteOriginal.IsChecked == false);
            Click(window.ClearButton);
            Check(window.SelectedPath is null && !window.ClearButton.IsEnabled && window.IsEncryptMode);
        }
        finally { window.Close(); }
    }

    private static void GuiDrop()
    {
        var window = OpenWindow();
        try
        {
            string path = Write("drop.txt", Sample);
            var data = new DataObject(DataFormats.FileDrop, new[] { path });
            var drop = CreateDragEvent(data, window, DragDrop.PreviewDropEvent);
            window.BrowseButton.RaiseEvent(drop);
            Check(drop.Handled && window.SelectedPath == path);
            var over = CreateDragEvent(data, window, DragDrop.PreviewDragOverEvent);
            window.Password.RaiseEvent(over);
            Check(over.Handled && over.Effects == DragDropEffects.Copy);
            Check(!window.AcceptDrop([path, path]) && !window.AcceptDrop([Root]) && !window.AcceptDrop([]));
            Check(window.SelectedPath == path);
            Check(!window.SelectFile(Path.Combine(Root, "missing.txt")));
        }
        finally { window.Close(); }
    }

    private static void GuiValidation()
    {
        var dialogs = new TestDialogs();
        var window = OpenWindow(Write("validation.txt", Sample), dialogs);
        try
        {
            Complete(window.RunOperationAsync());
            Check(window.StatusText.Text == "Введите непустой пароль" && FocusManager.GetFocusedElement(window) == window.Password);
            window.Password.Password = Password;
            window.Confirmation.Password = "не совпадает";
            Complete(window.RunOperationAsync());
            Check(window.StatusText.Text == "Пароли не совпадают" && FocusManager.GetFocusedElement(window) == window.Confirmation);
            Check(dialogs.DestinationCalls == 0 && !window.IsBusy);
        }
        finally { window.Close(); }
    }

    private static void GuiOperations()
    {
        string source = Write("gui-roundtrip.txt", Sample);
        string destination = Path.Combine(Root, "gui-roundtrip.pvlt");
        var dialogs = new TestDialogs { Destination = destination };
        var window = OpenWindow(source, dialogs);
        try
        {
            window.Password.Password = window.Confirmation.Password = Password;
            Task operation = window.RunOperationAsync();
            Check(window.IsBusy && !window.InputPanel.IsEnabled && !window.SettingsButton.IsEnabled);
            Check(window.OperationProgress.Visibility == Visibility.Visible && window.OperationProgress.IsIndeterminate);
            Check(window.Password.Password.Length == 0 && window.Confirmation.Password.Length == 0 && window.VisiblePassword.Text.Length == 0);
            string? screenshot = Environment.GetEnvironmentVariable("PVLT_SCREENSHOT");
            if (!string.IsNullOrEmpty(screenshot))
            {
                window.UpdateLayout();
                SaveScreenshot(window, Path.ChangeExtension(screenshot, "busy.png"), 1);
            }
            Check(!window.AcceptDrop([source]));
            Check(window.RunOperationAsync().IsCompleted);
            Check(dialogs.DestinationCalls == 1);
            window.Close();
            Check(window.IsVisible && window.IsBusy);
            Complete(operation);
            Check(File.Exists(destination) && File.Exists(source) && !window.IsBusy && window.InputPanel.IsEnabled);
            Check(window.OperationProgress.Visibility == Visibility.Collapsed && window.StatusText.Text.StartsWith("Готово", StringComparison.Ordinal));
            window.SelectFile(destination);
            string restored = Path.Combine(Root, "gui-restored");
            Directory.CreateDirectory(restored);
            dialogs.Destination = restored;
            window.Password.Password = Password;
            Complete(window.RunOperationAsync());
            Check(File.ReadAllBytes(Path.Combine(restored, "gui-roundtrip.txt")).SequenceEqual(Sample));
        }
        finally { window.Close(); }
    }

    private static void GuiDeletion()
    {
        string source = Write("gui-delete.txt", Sample), destination = Path.Combine(Root, "gui-delete.pvlt");
        var dialogs = new TestDialogs();
        var window = OpenWindow(source, dialogs);
        try
        {
            window.Password.Password = window.Confirmation.Password = Password;
            window.DeleteOriginal.IsChecked = true;
            Complete(window.RunOperationAsync());
            Check(!window.IsBusy && File.Exists(source) && dialogs.ConfirmationCalls == 0);
            dialogs.Destination = destination;
            Complete(window.RunOperationAsync());
            Check(!window.IsBusy && File.Exists(source) && !File.Exists(destination) && dialogs.ConfirmationCalls == 1);
            dialogs.AllowDelete = true;
            Complete(window.RunOperationAsync());
            Check(!File.Exists(source) && File.Exists(destination) && window.SelectedPath is null);
            Check(!window.ClearButton.IsEnabled && window.StatusText.Text.Contains("исходник удалён", StringComparison.Ordinal));
            using var restored = VaultFormat.Decrypt(File.ReadAllBytes(destination), Password);
            Check(restored.Data.SequenceEqual(Sample));
        }
        finally { window.Close(); }
    }

    private static void GuiErrors()
    {
        string destination = Path.Combine(Root, "gui-error-restored");
        Directory.CreateDirectory(destination);
        var window = OpenWindow(Write("gui-error.pvlt", VaultFormat.Encrypt("error.txt", Sample, Password)), new TestDialogs { Destination = destination });
        try
        {
            window.Password.Password = "неверный пароль";
            Complete(window.RunOperationAsync());
            Check(!window.IsBusy && window.InputPanel.IsEnabled && !Directory.EnumerateFileSystemEntries(destination).Any());
            Check(window.StatusText.Text == "Операция не завершена" && window.Password.Password.Length == 0);
            Check(FocusManager.GetFocusedElement(window) == window.Password);
            window.Password.Password = Password;
            Complete(window.RunOperationAsync());
            Check(File.ReadAllBytes(Path.Combine(destination, "error.txt")).SequenceEqual(Sample));
            window.Password.Password = Password;
            Complete(window.RunOperationAsync());
            Check(window.StatusText.Text == "Операция не завершена" && window.StatusDetail.Text.Contains("уже существует", StringComparison.Ordinal));
            Check(File.ReadAllBytes(Path.Combine(destination, "error.txt")).SequenceEqual(Sample));
        }
        finally { window.Close(); }
    }

    private static void GuiLayout()
    {
        var window = OpenWindow(Write("Личный документ с длинным именем для проверки размещения.txt", Sample));
        try
        {
            window.Password.Password = window.Confirmation.Password = Password;
            string? screenshot = Environment.GetEnvironmentVariable("PVLT_SCREENSHOT");
            foreach (double scale in new[] { 1, 1.25, 1.5, 2 })
            {
                window.UpdateLayout();
                CheckLayout(window);
                if (!string.IsNullOrEmpty(screenshot))
                {
                    SaveScreenshot(window, Path.ChangeExtension(screenshot, $"{scale * 100:0}.png"), scale);
                    if (scale == 1) SaveScreenshot(window, screenshot, scale);
                }
            }
            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            window.UpdateLayout();
            Check(window.EncryptButton.ActualWidth >= 200 && window.StatusText.ActualHeight > 0);
            window.Width = 680; window.Height = 620;
            window.SelectFile(Write("layout.pvlt", [0]));
            window.UpdateLayout();
            CheckLayout(window);
            // At 200%, a 1920×1080 display has less than 540 DIPs of working height.
            window.ApplyWorkArea(new Size(960, 500));
            window.UpdateLayout();
            Check(window.Height <= 500 && window.MaxHeight == 500 && window.MinHeight <= 500);
            Check(window.Body.ActualHeight > 0);
        }
        finally { window.Close(); }
    }

    private static void GuiLegacyContainer()
    {
        using var fixture = typeof(Program).Assembly.GetManifestResourceStream("PocketVault.LegacyContainer")!;
        using var reader = new StreamReader(fixture);
        string path = Write("legacy-v1.pvlt", Convert.FromBase64String(reader.ReadToEnd()));
        string destination = Path.Combine(Root, "legacy-restored");
        Directory.CreateDirectory(destination);
        var window = OpenWindow(path, new TestDialogs { Destination = destination });
        try
        {
            window.Password.Password = "Пароль совместимости 2026";
            Complete(window.RunOperationAsync());
            Check(File.ReadAllText(Path.Combine(destination, "наследие.txt"), Encoding.UTF8) == "PocketVault v1 — совместимость формата.\n");
        }
        finally { window.Close(); }
    }

    private static void GuiDialogs()
    {
        var window = OpenWindow();
        try
        {
            bool inspected = false;
            Exception? inspectionError = null;
            void InspectDialog()
            {
                var dialog = Application.Current.Windows.OfType<Window>().Single(w => w.Owner == window);
                try
                {
                    Check(((SolidColorBrush)dialog.Background).Color == Color.FromRgb(0x14, 0x14, 0x14));
                    Check(((SolidColorBrush)dialog.Foreground).Color == Color.FromRgb(0xF5, 0xF5, 0xF5));
                    Check(dialog.ActualWidth > 0 && dialog.ActualHeight > 0);
                    inspected = true;
                }
                catch (Exception ex) { inspectionError = ex; }
                finally { dialog.Close(); }
            }
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(InspectDialog));
            VaultDialogs.ShowSettings(window);
            if (inspectionError is not null) throw inspectionError;
            Check(inspected);
            inspected = false;
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(InspectDialog));
            Check(!new VaultDialogs().ConfirmDeletion(window));
            if (inspectionError is not null) throw inspectionError;
            Check(inspected);
        }
        finally { window.Close(); }
    }

    private static void CheckLayout(MainWindow window)
    {
        FrameworkElement[] controls = [window.BrowseButton, window.FileCard, window.Password, window.EncryptButton, window.DecryptButton, window.StatusCard];
        if (window.IsEncryptMode) controls = [.. controls, window.Confirmation, window.DeleteOriginal];
        foreach (var control in controls)
        {
            var bounds = control.TransformToAncestor(window).TransformBounds(new Rect(control.RenderSize));
            Check(bounds.Width > 0 && bounds.Height > 0 && bounds.Left >= 0 && bounds.Right <= window.ActualWidth + 1 && bounds.Bottom <= window.ActualHeight + 1);
        }
        Rect file = window.FileCard.TransformToAncestor(window).TransformBounds(new Rect(window.FileCard.RenderSize));
        Rect password = window.Password.TransformToAncestor(window).TransformBounds(new Rect(window.Password.RenderSize));
        Rect actions = window.EncryptButton.TransformToAncestor(window).TransformBounds(new Rect(window.EncryptButton.RenderSize));
        Rect status = window.StatusCard.TransformToAncestor(window).TransformBounds(new Rect(window.StatusCard.RenderSize));
        Check(file.Bottom < password.Top && password.Bottom < actions.Top && actions.Bottom < status.Top);
    }

    private static void CaptureDocumentation(string output, string samples)
    {
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(samples);
        string source = Path.Combine(samples, "project-notes.txt");
        string container = Path.Combine(samples, "project-notes.pvlt");
        File.WriteAllText(source, "PocketVault demonstration file. No private data.\n", Encoding.UTF8);
        var window = OpenWindow(source, new TestDialogs { Destination = container });
        try
        {
            // A normal user-resized window leaves room for the taller busy status card.
            window.Height = 680;
            const string demoPassword = "Public demo passphrase";
            window.Password.Password = window.Confirmation.Password = demoPassword;
            window.UpdateLayout();
            CheckLayout(window);
            SaveScreenshot(window, Path.Combine(output, "pocketvault-main.png"), 1.5);
            Task operation = window.RunOperationAsync();
            Check(window.IsBusy);
            window.UpdateLayout();
            CheckLayout(window);
            SaveScreenshot(window, Path.Combine(output, "pocketvault-processing.png"), 1.5);
            Complete(operation);
            Check(File.Exists(container) && window.StatusText.Text.StartsWith("Готово", StringComparison.Ordinal));
            Check(window.SelectFile(container));
            window.Password.Password = demoPassword;
            window.UpdateLayout();
            CheckLayout(window);
            SaveScreenshot(window, Path.Combine(output, "pocketvault-decrypt.png"), 1.5);
            Console.WriteLine("[OK] Three documentation screenshots rendered from the real WPF window at 144 DPI.");
        }
        finally { window.Close(); }
    }

    private static void SaveScreenshot(MainWindow window, string path, double scale)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth * scale), (int)Math.Ceiling(window.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static MainWindow OpenWindow(string? path = null, IVaultDialogs? dialogs = null)
    {
        var window = new MainWindow(path, dialogs ?? new TestDialogs());
        window.Show();
        PumpEvents();
        window.UpdateLayout();
        return window;
    }

    // WPF constructs OLE drag events internally; use its constructor to exercise the real routed handlers.
    private static DragEventArgs CreateDragEvent(IDataObject data, Window window, RoutedEvent routedEvent)
    {
        var args = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, [data, DragDropKeyStates.None, DragDropEffects.Copy, window, new Point(100, 100)], null)!;
        args.RoutedEvent = routedEvent;
        return args;
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void PumpEvents()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void Complete(Task operation)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!operation.IsCompleted && DateTime.UtcNow < deadline) { PumpEvents(); Thread.Sleep(10); }
        Check(operation.IsCompleted);
        operation.GetAwaiter().GetResult();
        PumpEvents();
    }

    private sealed class TestDialogs : IVaultDialogs
    {
        internal string? Destination { get; set; }
        internal bool AllowDelete { get; set; }
        internal int DestinationCalls { get; private set; }
        internal int ConfirmationCalls { get; private set; }
        public string? SelectFile(Window owner) => null;
        public string? SelectDestination(Window owner, string input, bool encrypt) { DestinationCalls++; return Destination; }
        public bool ConfirmDeletion(Window owner) { ConfirmationCalls++; return AllowDelete; }
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
