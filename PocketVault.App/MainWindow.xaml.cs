using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using PocketVault.Core;

namespace PocketVault.App;

public partial class MainWindow : Window
{
    private readonly IVaultDialogs dialogs;
    private bool showingPassword;
    private bool synchronizingPassword;
    private bool ready;
    internal string? SelectedPath { get; private set; }
    internal bool IsEncryptMode { get; private set; } = true;
    internal bool IsBusy { get; private set; }

    public MainWindow(string? initialFile = null) : this(initialFile, new VaultDialogs()) { }

    internal MainWindow(string? initialFile, IVaultDialogs dialogs)
    {
        this.dialogs = dialogs;
        InitializeComponent();
        using var icon = typeof(MainWindow).Assembly.GetManifestResourceStream("PocketVault.AppIcon")!;
        Icon = BitmapFrame.Create(icon, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        ready = true;
        if (!string.IsNullOrWhiteSpace(initialFile)) SelectFile(initialFile);
        UpdateControls();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyWorkArea(DisplayWorkArea.For(this));
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        // Apply after WPF accepts the monitor's suggested bounds.
        Dispatcher.BeginInvoke(new Action(() => ApplyWorkArea(DisplayWorkArea.For(this))));
    }

    internal void ApplyWorkArea(Size area)
    {
        MinWidth = Math.Min(560, area.Width);
        MinHeight = Math.Min(480, area.Height);
        MaxWidth = area.Width;
        MaxHeight = area.Height;
        Width = Math.Min(Width, area.Width);
        Height = Math.Min(Height, area.Height);
    }

    internal bool SelectFile(string path)
    {
        if (IsBusy) return false;
        try
        {
            var info = new FileInfo(Path.GetFullPath(path));
            if (!info.Exists) throw new FileNotFoundException();
            // Read metadata before committing the selection, so an inaccessible path cannot leave a partial UI state.
            long length = info.Length;
            ClearSecrets();
            SelectedPath = info.FullName;
            IsEncryptMode = !info.Extension.Equals(".pvlt", StringComparison.OrdinalIgnoreCase);
            DeleteOriginal.IsChecked = false;
            FileNameText.Text = info.Name;
            FileDetailText.Text = $"{FormatSize(length)} · {info.DirectoryName}";
            FileCard.ToolTip = info.FullName;
            SetStatus(IsEncryptMode ? "Файл готов к шифрованию" : "Контейнер готов к расшифровке", "Введите пароль" + (IsEncryptMode ? " и повторите его." : "."));
            UpdateControls();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            SetStatus("Не удалось выбрать файл", ErrorText(ex));
            return false;
        }
    }

    private static string FormatSize(long length) => length switch
    {
        >= 1024 * 1024 => (length / (1024d * 1024)).ToString("0.#", CultureInfo.GetCultureInfo("ru-RU")) + " МиБ",
        >= 1024 => (length / 1024d).ToString("0.#", CultureInfo.GetCultureInfo("ru-RU")) + " КиБ",
        _ => length + " Б"
    };

    private void UpdateControls()
    {
        if (!ready) return;
        ConfirmationPanel.Visibility = IsEncryptMode ? Visibility.Visible : Visibility.Collapsed;
        DeleteOriginal.Visibility = IsEncryptMode ? Visibility.Visible : Visibility.Collapsed;
        InputPanel.IsEnabled = SettingsButton.IsEnabled = !IsBusy;
        ClearButton.IsEnabled = SelectedPath is not null;
        bool canRun = !IsBusy && SelectedPath is not null && Password.Password.Length > 0;
        EncryptButton.IsEnabled = canRun && IsEncryptMode;
        DecryptButton.IsEnabled = canRun && !IsEncryptMode;
        EncryptButton.IsDefault = IsEncryptMode;
        DecryptButton.IsDefault = !IsEncryptMode;
        EncryptButton.ToolTip = IsEncryptMode ? "Зашифровать выбранный файл" : "Для шифрования выберите исходный файл";
        DecryptButton.ToolTip = IsEncryptMode ? "Для расшифровки выберите контейнер .pvlt" : "Расшифровать выбранный контейнер";
        OperationProgress.Visibility = IsBusy ? Visibility.Visible : Visibility.Collapsed;
        AnimateProgress();
    }

    private void ProgressSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ready) AnimateProgress();
    }

    private void AnimateProgress()
    {
        OperationProgress.ApplyTemplate();
        if (OperationProgress.Template.FindName("Activity", OperationProgress) is not System.Windows.Controls.Border activity
            || activity.RenderTransform is not TranslateTransform motion) return;
        if (motion.IsFrozen) { motion = motion.Clone(); activity.RenderTransform = motion; }
        motion.BeginAnimation(TranslateTransform.XProperty, IsBusy
            ? new DoubleAnimation(-120, Math.Max(120, OperationProgress.ActualWidth), TimeSpan.FromSeconds(1.4)) { RepeatBehavior = RepeatBehavior.Forever }
            : null);
    }

    private void SetStatus(string text, string detail)
    {
        StatusText.Text = text;
        StatusDetail.Text = detail;
        StatusCard.ToolTip = detail;
    }

    private void ClearSecrets()
    {
        showingPassword = false;
        Password.Clear();
        VisiblePassword.Clear();
        Confirmation.Clear();
        VisiblePassword.Visibility = Visibility.Collapsed;
        Password.Visibility = Visibility.Visible;
        ShowPasswordButton.ToolTip = "Показать пароль";
        System.Windows.Automation.AutomationProperties.SetName(ShowPasswordButton, "Показать пароль");
    }

    private void PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (ready && !synchronizingPassword && ReferenceEquals(sender, Password))
        {
            synchronizingPassword = true;
            try { VisiblePassword.Text = showingPassword ? Password.Password : string.Empty; }
            finally { synchronizingPassword = false; }
        }
        UpdateControls();
    }

    private void VisiblePasswordChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!ready || !showingPassword || synchronizingPassword) return;
        synchronizingPassword = true;
        try { Password.Password = VisiblePassword.Text; }
        finally { synchronizingPassword = false; }
        UpdateControls();
    }

    private void ShowPasswordClick(object sender, RoutedEventArgs e)
    {
        showingPassword = !showingPassword;
        synchronizingPassword = true;
        try { VisiblePassword.Text = showingPassword ? Password.Password : string.Empty; }
        finally { synchronizingPassword = false; }
        VisiblePassword.Visibility = showingPassword ? Visibility.Visible : Visibility.Collapsed;
        Password.Visibility = showingPassword ? Visibility.Collapsed : Visibility.Visible;
        string label = showingPassword ? "Скрыть пароль" : "Показать пароль";
        ShowPasswordButton.ToolTip = label;
        System.Windows.Automation.AutomationProperties.SetName(ShowPasswordButton, label);
        if (showingPassword) { VisiblePassword.Focus(); VisiblePassword.CaretIndex = VisiblePassword.Text.Length; }
        else Password.Focus();
    }

    private void DeleteChanged(object sender, RoutedEventArgs e)
    {
        if (ready && DeleteOriginal.IsChecked == true)
            SetStatus("Удаление только после проверки", "Обычное удаление не гарантирует уничтожения следов на диске. Перед началом потребуется подтверждение.");
        else if (ready) SetStatus("Готов к работе", "Полностью локально · формат .pvlt · до 128 МиБ");
    }

    private void BrowseClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string? path = dialogs.SelectFile(this);
            if (path is not null && SelectFile(path)) Password.Focus();
        }
        catch (Exception ex) { SetStatus("Не удалось выбрать файл", ErrorText(ex)); }
    }

    private void ClearClick(object sender, RoutedEventArgs e)
    {
        if (IsBusy) return;
        SelectedPath = null;
        IsEncryptMode = true;
        ClearSecrets();
        DeleteOriginal.IsChecked = false;
        FileNameText.Text = "Файл не выбран";
        FileDetailText.Text = "Выберите файл или контейнер .pvlt";
        FileCard.ToolTip = null;
        SetStatus("Готов к работе", "Полностью локально · формат .pvlt · до 128 МиБ");
        UpdateControls();
        BrowseButton.Focus();
    }

    internal bool AcceptDrop(string[] paths)
    {
        if (IsBusy) return false;
        if (paths.Length != 1 || !File.Exists(paths[0]))
        {
            SetStatus("Перетащите один обычный файл", "Папки и несколько файлов одновременно не поддерживаются.");
            return false;
        }
        bool selected = SelectFile(paths[0]);
        if (selected) Password.Focus();
        return selected;
    }

    private void FileDragOver(object sender, DragEventArgs e)
    {
        bool allowed = !IsBusy && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } paths && File.Exists(paths[0]);
        e.Effects = allowed ? DragDropEffects.Copy : DragDropEffects.None;
        BrowseButton.Background = allowed ? (Brush)FindResource("Surface") : Brushes.Transparent;
        e.Handled = true;
    }

    private void FileDragLeave(object sender, DragEventArgs e) => BrowseButton.Background = Brushes.Transparent;

    private void FileDrop(object sender, DragEventArgs e)
    {
        BrowseButton.Background = Brushes.Transparent;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) AcceptDrop(paths);
        e.Handled = true;
    }

    private async void EncryptClick(object sender, RoutedEventArgs e) => await RunOperationAsync();
    private async void DecryptClick(object sender, RoutedEventArgs e) => await RunOperationAsync();

    internal async Task RunOperationAsync()
    {
        if (IsBusy) return;
        if (SelectedPath is null || !File.Exists(SelectedPath)) { SetStatus("Выберите существующий файл", "Файл мог быть перемещён или удалён."); BrowseButton.Focus(); return; }
        if (Password.Password.Length == 0) { SetStatus("Введите непустой пароль", "Пробелы и регистр имеют значение."); Password.Focus(); return; }
        if (IsEncryptMode && Password.Password != Confirmation.Password)
        {
            SetStatus("Пароли не совпадают", "Повторите пароль ещё раз.");
            Confirmation.Focus();
            return;
        }

        bool encrypt = IsEncryptMode;
        bool remove = encrypt && DeleteOriginal.IsChecked == true;
        string input = SelectedPath;
        string? destination;
        try
        {
            destination = dialogs.SelectDestination(this, input, encrypt);
            if (destination is null) return;
            if (remove && !dialogs.ConfirmDeletion(this)) return;
        }
        catch (Exception ex) { SetStatus("Не удалось выбрать место сохранения", ErrorText(ex)); return; }

        string secret = Password.Password;
        ClearSecrets();
        IsBusy = true;
        UpdateControls();
        SetStatus(encrypt ? "Шифрование…" : "Расшифровка…", encrypt
            ? (remove ? "Шифрование, сохранение и проверка перед удалением исходника…" : "Упаковка, шифрование и сохранение контейнера…")
            : "Проверка контейнера и восстановление файла…");
        try
        {
            string result = await Task.Run(() =>
            {
                if (!encrypt) return VaultFiles.Decrypt(input, destination, secret);
                VaultFiles.Encrypt(input, destination, secret, remove);
                return destination;
            });
            if (remove)
            {
                SelectedPath = null;
                FileNameText.Text = "Исходный файл удалён";
                FileDetailText.Text = "Сохранённый контейнер проверен";
                FileCard.ToolTip = null;
                DeleteOriginal.IsChecked = false;
            }
            SetStatus(remove ? "Готово · контейнер проверен, исходник удалён" : "Готово · файл сохранён", result);
        }
        catch (Exception ex) { SetStatus("Операция не завершена", ErrorText(ex)); }
        finally
        {
            secret = string.Empty;
            IsBusy = false;
            UpdateControls();
            if (SelectedPath is null) BrowseButton.Focus(); else Password.Focus();
        }
    }

    internal static string ErrorText(Exception error) => error switch
    {
        VaultException => error.Message,
        FileNotFoundException or DirectoryNotFoundException => "Файл или папка больше не существуют.",
        UnauthorizedAccessException => "Нет прав доступа к файлу или папке.",
        ArgumentException or NotSupportedException => "Не удалось прочитать путь к файлу.",
        IOException io when (io.HResult & 0xFFFF) is 112 or 39 => "Недостаточно свободного места на диске.",
        IOException => "Не удалось прочитать или сохранить файл. Проверьте доступ, свободное место и совпадение имён.",
        OutOfMemoryException => "Недостаточно оперативной памяти. Закройте другие программы или выберите меньший файл.",
        _ => "Операция не завершена. Проверьте файл и доступ к выбранной папке."
    };

    private void SettingsClick(object sender, RoutedEventArgs e) => VaultDialogs.ShowSettings(this);
    private void MinimizeClick(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    private void MaximizeClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
    private void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (!IsBusy) return;
        e.Cancel = true;
        SetStatus("Дождитесь завершения операции", "Файл обрабатывается. Окно можно свернуть.");
    }
    private void WindowClosed(object? sender, EventArgs e) => ClearSecrets();
    private void WindowLoaded(object sender, RoutedEventArgs e)
    {
        if (SelectedPath is null) BrowseButton.Focus(); else Password.Focus();
    }
}
