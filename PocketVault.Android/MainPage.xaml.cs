using System.Text.Json;
using PocketVault.Android.Diagnostics;

namespace PocketVault.Android;

public partial class MainPage : ContentPage
{
    private bool isRunning;

    public MainPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
#if DEBUG
        if (SmokeTestRequest.RunId is { } runId)
        {
            SmokeTestRequest.RunId = null;
            await RunSmokeTestAsync(runId);
        }
#endif
    }

    private async void OnCheckClicked(object? sender, EventArgs e) =>
        await RunSmokeTestAsync(Guid.NewGuid().ToString("N"));

    private async Task RunSmokeTestAsync(string runId)
    {
        if (isRunning) return;
        isRunning = true;
        CheckButton.IsEnabled = false;
        ProgressIndicator.IsVisible = ProgressIndicator.IsRunning = true;
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
            global::Android.Util.Log.Error("PocketVaultCrypto", $"{runId}: {ex}");
        }
        finally
        {
            ProgressIndicator.IsVisible = ProgressIndicator.IsRunning = false;
            CheckButton.IsEnabled = true;
            isRunning = false;
        }
    }
}
