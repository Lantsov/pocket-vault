using PocketVault.Android.Services;
using PocketVault.Core;

namespace PocketVault.Android;

public partial class MainPage : ContentPage
{
    private bool isRunning;
    private bool initialized;
    private readonly IDocumentAccess documents = new SafDocumentAccess();
    private readonly DocumentOperations operations;
    private VaultDocument? selected;

    public MainPage()
    {
        InitializeComponent();
        operations = new DocumentOperations(documents);
        initialized = true;
        UpdateValidation();
    }

    private bool CanRun => selected is not null && !string.IsNullOrEmpty(PasswordEntry.Text) &&
        (selected.IsContainer || PasswordEntry.Text == ConfirmationEntry.Text);

    internal async Task OpenContainerAsync(string uri)
    {
        if (isRunning) return; // Never replace the source of an operation in progress.
        SetBusy(true);
        ClearSensitiveData();
        try
        {
            selected = await SafDocumentAccess.OpenContainerAsync(uri);
            UpdateSelection();
            SetStatus("Контейнер выбран", "Имя и содержимое скрыты до проверки пароля.");
        }
        catch (Exception ex) { SetStatus("Не удалось открыть контейнер", UserMessage(ex)); }
        finally { SetBusy(false); }
    }

    private async void OnPickClicked(object? sender, EventArgs e)
    {
        if (isRunning) return;
        SetBusy(true);
        try
        {
            await HideKeyboardAsync();
            ClearSensitiveData();
            SetStatus("Выбор файла", "Откройте документ в системном диалоге.");
            var document = await documents.PickAsync();
            if (document is null)
            {
                SetStatus("Выбор отменён", selected is null ? "Файл не выбран. Можно попробовать снова." : "Предыдущий файл остаётся выбранным.");
                return;
            }
            selected = document;
            UpdateSelection();
            SetStatus(document.IsContainer ? "Контейнер выбран" : "Файл готов к шифрованию",
                document.IsContainer ? "Имя и содержимое скрыты до проверки пароля." : "Задайте пароль. Восстановить его не получится.");
            AnimateCard(SelectedCard);
        }
        catch (Exception ex) { SetStatus("Не удалось выбрать файл", UserMessage(ex)); }
        finally { SetBusy(false); }
    }

    private void OnClearClicked(object? sender, EventArgs e)
    {
        if (isRunning) return;
        ClearSensitiveData();
        selected = null;
        UpdateSelection();
        SetStatus("Файл не выбран", "Выберите один документ из памяти устройства.");
        AnimateCard(PickerCard);
    }

    private void UpdateSelection()
    {
        bool hasFile = selected is not null;
        PickerCard.IsVisible = !hasFile;
        SelectedCard.IsVisible = PickAnotherButton.IsVisible = hasFile;
        FileLabel.Text = selected?.Name;
        FileSizeLabel.Text = selected?.Size is long size ? FormatSize(size) : "Размер неизвестен · лимит проверяется при чтении";
        bool decrypt = selected?.IsContainer == true;
        ConfirmationGroup.IsVisible = !decrypt;
        PasswordEntry.ReturnType = decrypt ? ReturnType.Done : ReturnType.Next;
        ModeLabel.Text = !hasFile ? "Один файл · до 32 МиБ" : decrypt ? "Расшифровка · .pvlt v1" : "Шифрование · до 32 МиБ";
        OperationButton.Text = decrypt ? "Расшифровать" : "Зашифровать";
        UpdateValidation();
    }

    private static string FormatSize(long size) => size switch
    {
        >= 1024 * 1024 => $"{size / (1024d * 1024):0.##} МиБ",
        >= 1024 => $"{size / 1024d:0.##} КиБ",
        _ => $"{size:N0} Б"
    };

    private void OnPasswordChanged(object? sender, TextChangedEventArgs e)
    {
        if (initialized) UpdateValidation();
    }

    private void UpdateValidation()
    {
        if (!initialized) return;
        bool decrypt = selected?.IsContainer == true;
        string password = PasswordEntry.Text ?? "";
        string confirmation = ConfirmationEntry.Text ?? "";
        bool mismatch = !decrypt && confirmation.Length > 0 && password != confirmation;
        ValidationLabel.Text = isRunning ? "Пароли очищены из полей." : password.Length == 0
            ? decrypt ? "Введите пароль от контейнера." : "Введите пароль и повторите его."
            : mismatch ? "Пароли не совпадают."
            : !decrypt && confirmation.Length == 0 ? "Повторите пароль."
            : selected is null ? "Теперь выберите файл." : "Всё готово. Можно продолжить.";
        ConfirmationBorder.Stroke = Color.FromArgb(mismatch ? "#A0A0A0" : ConfirmationEntry.IsFocused ? "#F5F5F5" : "#353535");
        OperationButton.IsEnabled = !isRunning && CanRun;
    }

    private async void OnOperationClicked(object? sender, EventArgs e)
    {
        if (isRunning || !CanRun || selected is null) return;
        var document = selected;
        string password = PasswordEntry.Text ?? "";
        string confirmation = ConfirmationEntry.Text ?? "";
        SetBusy(true);
        try
        {
            await HideKeyboardAsync();
            ClearSensitiveData();
            OperationButton.Text = document.IsContainer ? "Расшифровка…" : "Шифрование…";
            SetStatus(document.IsContainer ? "Расшифровка…" : "Шифрование…", "После обработки выберите, куда сохранить результат.");
            await FormScroll.ScrollToAsync(StatusCard, ScrollToPosition.MakeVisible, true);
            bool saved = await operations.RunAsync(document, password, confirmation);
            SetStatus(saved ? "Готово. Файл сохранён" : "Сохранение отменено",
                saved ? "Исходный документ остался без изменений." : "Результат не записан. Чтобы повторить, введите пароль заново.", saved);
            if (saved) TrySuccessHaptic();
        }
        catch (Exception ex) { SetStatus("Не удалось завершить операцию", UserMessage(ex)); }
        finally
        {
            password = confirmation = ""; // Managed strings cannot be reliably erased.
            ClearSensitiveData();
            SetBusy(false);
            OperationButton.Text = document.IsContainer ? "Расшифровать" : "Зашифровать";
        }
    }

    internal void ClearSensitiveData()
    {
        PasswordEntry.Text = ConfirmationEntry.Text = "";
        PasswordEntry.IsPassword = ConfirmationEntry.IsPassword = true;
        UpdateVisibilityButtons();
        PasswordEntry.Unfocus();
        ConfirmationEntry.Unfocus();
    }

    private void OnTogglePassword(object? sender, EventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        UpdateVisibilityButtons();
    }

    private void OnToggleConfirmation(object? sender, EventArgs e)
    {
        ConfirmationEntry.IsPassword = !ConfirmationEntry.IsPassword;
        UpdateVisibilityButtons();
    }

    private void UpdateVisibilityButtons()
    {
        PasswordVisibilityButton.Source = PasswordEntry.IsPassword ? "pv_eye.png" : "pv_eyeoff.png";
        ConfirmationVisibilityButton.Source = ConfirmationEntry.IsPassword ? "pv_eye.png" : "pv_eyeoff.png";
        SemanticProperties.SetDescription(PasswordVisibilityButton, PasswordEntry.IsPassword ? "Показать пароль" : "Скрыть пароль");
        SemanticProperties.SetDescription(ConfirmationVisibilityButton, ConfirmationEntry.IsPassword ? "Показать повтор пароля" : "Скрыть повтор пароля");
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        if (isRunning) return;
        SettingsButton.IsEnabled = false;
        try
        {
            await HideKeyboardAsync();
            ClearSensitiveData();
            await Navigation.PushAsync(new SettingsPage());
        }
        finally { SettingsButton.IsEnabled = true; }
    }

    private void OnPasswordCompleted(object? sender, EventArgs e)
    {
        if (selected?.IsContainer == true) OnOperationClicked(sender, e);
        else ConfirmationEntry.Focus();
    }

    private void OnConfirmationCompleted(object? sender, EventArgs e) => OnOperationClicked(sender, e);

    private void OnEntryFocused(object? sender, FocusEventArgs e)
    {
        (sender == PasswordEntry ? PasswordBorder : ConfirmationBorder).Stroke = Color.FromArgb("#F5F5F5");
    }

    private void OnEntryUnfocused(object? sender, FocusEventArgs e)
    {
        PasswordBorder.Stroke = Color.FromArgb("#353535");
        UpdateValidation();
    }

    private async Task HideKeyboardAsync()
    {
        if (PasswordEntry.IsSoftInputShowing()) await PasswordEntry.HideSoftInputAsync(CancellationToken.None);
        if (ConfirmationEntry.IsSoftInputShowing()) await ConfirmationEntry.HideSoftInputAsync(CancellationToken.None);
    }

    private void SetBusy(bool busy)
    {
        isRunning = busy;
        PickButton.IsEnabled = PickAnotherButton.IsEnabled = ClearButton.IsEnabled = SettingsButton.IsEnabled =
            PasswordEntry.IsEnabled = ConfirmationEntry.IsEnabled = PasswordVisibilityButton.IsEnabled =
            ConfirmationVisibilityButton.IsEnabled = !busy;
        ProgressIndicator.IsVisible = ProgressIndicator.IsRunning = busy;
        StatusIcon.IsVisible = !busy;
        UpdateValidation();
    }

    private void SetStatus(string title, string detail, bool success = false)
    {
        StatusTitle.Text = title;
        ResultLabel.Text = detail;
        StatusIcon.Source = success ? "pv_check.png" : "pv_info.png";
    }

    private static void AnimateCard(VisualElement card)
    {
        if (!global::Android.Animation.ValueAnimator.AreAnimatorsEnabled()) return;
        card.Opacity = 0.4;
        _ = card.FadeToAsync(1, 180, Easing.CubicOut);
    }

    private static void TrySuccessHaptic()
    {
        try
        {
            // View feedback respects system haptic settings and needs no VIBRATE permission.
            Platform.CurrentActivity?.Window?.DecorView?.PerformHapticFeedback(
                OperatingSystem.IsAndroidVersionAtLeast(30)
                    ? global::Android.Views.FeedbackConstants.Confirm
                    : global::Android.Views.FeedbackConstants.ContextClick);
        }
        catch (Exception) { /* Haptics are optional; the file was already saved. */ }
    }

    private static string UserMessage(Exception ex) => ex switch
    {
        VaultException or DocumentWriteException => ex.Message,
        OperationCanceledException => "Системный диалог закрыт. Операция отменена.",
        UnauthorizedAccessException or System.Security.SecurityException or Java.Lang.SecurityException =>
            "Нет доступа к документу. Выберите файл заново и проверьте место сохранения.",
        IOException => "Не удалось прочитать файл. Проверьте его доступность и выберите заново.",
        OutOfMemoryException => "Недостаточно памяти. Выберите файл меньшего размера или закройте другие приложения.",
        _ => "Не удалось обработать документ. Проверьте доступ к файлу и месту сохранения, затем повторите."
    };

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        Form.WidthRequest = Math.Min(width, 560);
    }

    protected override void OnDisappearing()
    {
        ClearSensitiveData();
        base.OnDisappearing();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
#if DEBUG
        if (Diagnostics.SmokeTestRequest.DocumentRunId is { } documentRunId)
        {
            Diagnostics.SmokeTestRequest.DocumentRunId = null;
            await RunDocumentChecksAsync(documentRunId);
        }
        if (Diagnostics.SmokeTestRequest.RunId is { } runId)
        {
            Diagnostics.SmokeTestRequest.RunId = null;
            await RunSmokeTestAsync(runId);
        }
#else
        await Task.CompletedTask;
#endif
    }
}
