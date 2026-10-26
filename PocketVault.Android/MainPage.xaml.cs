using System.Text.Json;
using PocketVault.Android.Diagnostics;
using PocketVault.Android.Services;
using PocketVault.Core;

namespace PocketVault.Android;

public partial class MainPage : ContentPage
{
    private bool isRunning;
    private readonly IDocumentAccess documents = new SafDocumentAccess();
    private readonly DocumentOperations operations;
    private VaultDocument? selected;

    public MainPage()
    {
        InitializeComponent();
        operations = new DocumentOperations(documents);
    }

    private async void OnPickClicked(object? sender, EventArgs e)
    {
        if (isRunning) return;
        SetBusy(true);
        ClearPasswords();
        try
        {
            var document = await documents.PickAsync();
            if (document is null) { ResultLabel.Text = "Выбор файла отменён."; return; }
            selected = document;
            FileLabel.Text = $"{document.Name}\n{(document.Size is long size ? $"{size:N0} байт" : "Размер неизвестен; при чтении проверяется лимит.")}";
            ConfirmationEntry.IsVisible = !document.IsContainer;
            ModeLabel.Text = document.IsContainer ? "Расшифровка .pvlt" : "Шифрование файла";
            OperationButton.Text = document.IsContainer ? "Расшифровать" : "Зашифровать";
            ResultLabel.Text = "Введите пароль и запустите операцию.";
        }
        catch (Exception ex) { ResultLabel.Text = UserMessage(ex); }
        finally { SetBusy(false); }
    }

    private async void OnOperationClicked(object? sender, EventArgs e)
    {
        if (isRunning || selected is null) return;
        SetBusy(true);
        string password = PasswordEntry.Text ?? "";
        string confirmation = ConfirmationEntry.Text ?? "";
        ClearPasswords();
        ResultLabel.Text = "Обработка файла… Затем выберите место сохранения.";
        try
        {
            bool saved = await operations.RunAsync(selected, password, confirmation);
            ResultLabel.Text = saved ? "Готово. Файл сохранён. Исходный документ сохранён."
                : "Сохранение отменено. Выходной файл не записан.";
        }
        catch (Exception ex) { ResultLabel.Text = UserMessage(ex); }
        finally
        {
            password = confirmation = ""; // Managed strings cannot be reliably erased.
            SetBusy(false);
        }
    }

    private void ClearPasswords() => PasswordEntry.Text = ConfirmationEntry.Text = "";

    private void SetBusy(bool busy)
    {
        isRunning = busy;
        PickButton.IsEnabled = CheckButton.IsEnabled = PasswordEntry.IsEnabled = ConfirmationEntry.IsEnabled = !busy;
        OperationButton.IsEnabled = !busy && selected is not null;
        ProgressIndicator.IsVisible = ProgressIndicator.IsRunning = busy;
    }

    private static string UserMessage(Exception ex) => ex switch
    {
        VaultException or DocumentWriteException => ex.Message,
        OperationCanceledException => "Системный диалог закрыт. Операция отменена.",
        UnauthorizedAccessException or System.Security.SecurityException or Java.Lang.SecurityException =>
            "Нет разрешения на доступ к документу. Выберите файл заново и проверьте доступ к месту сохранения.",
        OutOfMemoryException => "Недостаточно памяти. Выберите файл меньшего размера или закройте другие приложения.",
        _ => "Не удалось обработать документ. Проверьте разрешения и доступность провайдера, затем выберите файл заново."
    };

    protected override async void OnAppearing()
    {
        base.OnAppearing();
#if DEBUG
        if (SmokeTestRequest.DocumentRunId is { } documentRunId)
        {
            SmokeTestRequest.DocumentRunId = null;
            await RunDocumentChecksAsync(documentRunId);
        }
        if (SmokeTestRequest.RunId is { } runId)
        {
            SmokeTestRequest.RunId = null;
            await RunSmokeTestAsync(runId);
        }
#endif
    }

#if DEBUG
    private async Task RunDocumentChecksAsync(string runId)
    {
        if (isRunning) return;
        SetBusy(true);
        ClearPasswords();
        ResultLabel.Text = "Синтетические проверки файловых операций…";
        try
        {
            if (!OperatingSystem.IsAndroid()) throw new PlatformNotSupportedException();
            var checks = await Task.Run(PocketVault.Tests.DocumentOperationChecks.RunAsync);
            var report = new SmokeReport(runId, DateTimeOffset.UtcNow,
                $"Android {(global::Android.OS.Build.VERSION.Release)}; .NET {Environment.Version}",
                (int)global::Android.OS.Build.VERSION.SdkInt, checks.All(c => c.Success),
                checks.Select(c => new SmokeCheck(c.Name, c.Success, c.Detail)).ToArray());
            ResultLabel.Text = $"Проверки документов: {checks.Count(c => c.Success)}/{checks.Count}";
            await File.WriteAllTextAsync(Path.Combine(FileSystem.AppDataDirectory, "document-smoke-result.json"),
                JsonSerializer.Serialize(report, SmokeReportJsonContext.Default.SmokeReport));
        }
        catch (Exception ex) { ResultLabel.Text = UserMessage(ex); }
        finally { SetBusy(false); }
    }
#endif

    private async void OnCheckClicked(object? sender, EventArgs e) =>
        await RunSmokeTestAsync(Guid.NewGuid().ToString("N"));

    private async Task RunSmokeTestAsync(string runId)
    {
        if (isRunning) return;
        SetBusy(true);
        ClearPasswords();
        ResultLabel.Text = "Проверка на Android runtime…";
        try
        {
            var report = await Task.Run(() => CryptoSmokeTest.Run(runId));
            ResultLabel.Text = $"{(report.Success ? "Проверка пройдена" : "Проверка не пройдена")}\n{report.Runtime}\n\n" +
                string.Join("\n", report.Checks.Select(check => $"{(check.Success ? "✓" : "✗")} {check.Name}: {check.Detail}"));
            string json = JsonSerializer.Serialize(report, SmokeReportJsonContext.Default.SmokeReport);
            await File.WriteAllTextAsync(Path.Combine(FileSystem.AppDataDirectory, "crypto-smoke-result.json"), json);
            global::Android.Util.Log.Info("PocketVaultCrypto", $"{runId}: {(report.Success ? "PASS" : "FAIL")}");
        }
        catch (Exception ex)
        {
            ResultLabel.Text = $"Ошибка проверки: {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }
}
