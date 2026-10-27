using PocketVault.Android.Services;
using PocketVault.Core;
using PocketVault.Android.Localization;
using System.Security.Cryptography;

namespace PocketVault.Android;

public partial class MainPage : ContentPage
{
    private static LocalizationManager L => LocalizationManager.Current;
    private string statusTitleKey = "LocalOnly";
    private string statusDetailKey = "LocalOnlyDetail";
    private bool statusSuccess;
    private string renderedLanguage = L.Culture.Name;
    private UI.KeyboardViewport? keyboardViewport;
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
        UpdateSelection();
        UpdateVisibilityButtons();
        Loaded += (_, _) => AttachKeyboardViewport();
        Unloaded += (_, _) => DetachKeyboardViewport();
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
            SetStatus("ContainerSelected", "ContainerPrivacy");
        }
        catch (Exception ex) { SetStatus("ContainerOpenFailed", UserMessage(ex)); }
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
            SetStatus("ChoosingFile", "ChoosingFileDetail");
            var document = await documents.PickAsync();
            if (document is null)
            {
                SetStatus("SelectionCancelled", selected is null ? "NoFileRetry" : "PreviousFileKept");
                return;
            }
            selected = document;
            UpdateSelection();
            SetStatus(document.IsContainer ? "ContainerSelected" : "ReadyToEncrypt",
                document.IsContainer ? "ContainerPrivacy" : "SetPasswordWarning");
            AnimateCard(SelectedCard);
        }
        catch (Exception ex) { SetStatus("SelectionFailed", UserMessage(ex)); }
        finally { SetBusy(false); }
    }

    private void OnClearClicked(object? sender, EventArgs e)
    {
        if (isRunning) return;
        ClearSensitiveData();
        selected = null;
        UpdateSelection();
        SetStatus("NoFile", "SelectOneDocument");
        AnimateCard(PickerCard);
    }

    private void UpdateSelection()
    {
        bool hasFile = selected is not null;
        PickerCard.IsVisible = !hasFile;
        SelectedCard.IsVisible = PickAnotherButton.IsVisible = hasFile;
        FileLabel.Text = selected?.Name;
        FileSizeLabel.Text = selected?.Size is long size ? FormatSize(size) : L["UnknownSize"];
        bool decrypt = selected?.IsContainer == true;
        ConfirmationGroup.IsVisible = !decrypt;
        PasswordEntry.ReturnType = decrypt ? ReturnType.Done : ReturnType.Next;
        ModeLabel.Text = L[!hasFile ? "FileLimit" : decrypt ? "DecryptMode" : "EncryptMode"];
        OperationButton.Text = L[decrypt ? "Decrypt" : "Encrypt"];
        UpdateValidation();
    }

    private static string FormatSize(long size) => size switch
    {
        >= 1024 * 1024 => L.Format("SizeMiB", size / (1024d * 1024)),
        >= 1024 => L.Format("SizeKiB", size / 1024d),
        _ => L.Format("SizeBytes", size)
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
        ValidationLabel.Text = L[isRunning ? "PasswordsCleared" : password.Length == 0
            ? decrypt ? "EnterContainerPassword" : "EnterAndConfirm"
            : mismatch ? "PasswordMismatch"
            : !decrypt && confirmation.Length == 0 ? "RepeatPassword"
            : selected is null ? "NowSelectFile" : "ReadyToContinue"];
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
            OperationButton.Text = L[document.IsContainer ? "Decrypting" : "Encrypting"];
            SetStatus(document.IsContainer ? "Decrypting" : "Encrypting", "ChooseSaveAfterProcessing");
            await FormScroll.ScrollToAsync(StatusCard, ScrollToPosition.MakeVisible, true);
            bool saved = await operations.RunAsync(document, password, confirmation);
            SetStatus(saved ? "FileSaved" : "SaveCancelled",
                saved ? "OriginalUnchanged" : "NotSavedRetry", saved);
            if (saved) TrySuccessHaptic();
        }
        catch (Exception ex) { SetStatus("OperationFailed", UserMessage(ex)); }
        finally
        {
            password = confirmation = ""; // Managed strings cannot be reliably erased.
            ClearSensitiveData();
            SetBusy(false);
            OperationButton.Text = L[document.IsContainer ? "Decrypt" : "Encrypt"];
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
        SemanticProperties.SetDescription(PasswordVisibilityButton, L[PasswordEntry.IsPassword ? "ShowPassword" : "HidePassword"]);
        SemanticProperties.SetDescription(ConfirmationVisibilityButton, L[ConfirmationEntry.IsPassword ? "ShowConfirmation" : "HideConfirmation"]);
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
        statusTitleKey = title;
        statusDetailKey = detail;
        statusSuccess = success;
        StatusTitle.Text = L[title];
        ResultLabel.Text = L[detail];
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

    // Do not parse localized exception messages or expose internal/provider text.
    private string UserMessage(Exception ex) => ex switch
    {
        VaultException { InnerException: CryptographicException } => "IncorrectPassword",
        VaultException => selected?.IsContainer == true ? "InvalidContainer" : "InvalidInput",
        DocumentWriteException => "WriteFailed",
        OperationCanceledException => "DialogCancelled",
        UnauthorizedAccessException or System.Security.SecurityException or Java.Lang.SecurityException => "AccessDenied",
        IOException => "ReadFailed",
        OutOfMemoryException => "NotEnoughMemory",
        _ => "DocumentFailed"
    };

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        renderedLanguage = L.Culture.Name;
        ClearSensitiveData();
        UpdateSelection();
        SetStatus(statusTitleKey, statusDetailKey, statusSuccess);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        Form.WidthRequest = Math.Min(width, 560);
        // Preserve the full brand at large system text sizes, without shrinking text.
        double fontScale = global::Android.App.Application.Context.Resources?.Configuration?.FontScale ?? 1;
        bool stackedHeader = Math.Min(width, 560) - 144 < 11 * 12 * fontScale;
        Grid.SetColumnSpan(BrandLabel, stackedHeader ? 2 : 1);
        Grid.SetRow(SettingsButton, stackedHeader ? 1 : 0);
    }

    protected override void OnDisappearing()
    {
        DetachKeyboardViewport();
        L.LanguageChanged -= OnLanguageChanged;
        ClearSensitiveData();
        base.OnDisappearing();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Dispatcher.Dispatch(AttachKeyboardViewport);
        L.LanguageChanged -= OnLanguageChanged;
        L.LanguageChanged += OnLanguageChanged;
        L.RefreshSystemLanguage();
        if (renderedLanguage != L.Culture.Name) OnLanguageChanged(this, EventArgs.Empty);
#if DEBUG
        Diagnostics.TypographyAudit.Schedule(this, "main");
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

    private void AttachKeyboardViewport()
    {
        if (keyboardViewport is null && FormScroll.Handler?.PlatformView is global::Android.Views.View)
            keyboardViewport = new UI.KeyboardViewport(this, FormScroll,
                () => PasswordEntry.IsFocused ? PasswordEntry : ConfirmationEntry.IsFocused ? ConfirmationEntry : null);
    }

    private void DetachKeyboardViewport()
    {
        keyboardViewport?.Detach();
        keyboardViewport = null;
    }
}
