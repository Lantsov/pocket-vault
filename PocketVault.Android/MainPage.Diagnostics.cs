#if DEBUG
using System.Text.Json;
using PocketVault.Android.Diagnostics;

namespace PocketVault.Android;

public partial class MainPage
{
    private async Task RunDocumentChecksAsync(string runId)
    {
        if (isRunning) return;
        SetBusy(true);
        ClearSensitiveData();
        SetStatus("Проверка Debug", "Синтетические проверки файловых операций…");
        try
        {
            if (!OperatingSystem.IsAndroid()) throw new PlatformNotSupportedException();
            var checks = await Task.Run(PocketVault.Tests.DocumentOperationChecks.RunAsync);
            var report = new SmokeReport(runId, DateTimeOffset.UtcNow,
                $"Android {(global::Android.OS.Build.VERSION.Release)}; .NET {Environment.Version}",
                (int)global::Android.OS.Build.VERSION.SdkInt, checks.All(c => c.Success),
                checks.Select(c => new SmokeCheck(c.Name, c.Success, c.Detail)).ToArray());
            SetStatus("Проверки документов", $"{checks.Count(c => c.Success)}/{checks.Count}", report.Success);
            await File.WriteAllTextAsync(Path.Combine(FileSystem.AppDataDirectory, "document-smoke-result.json"),
                JsonSerializer.Serialize(report, SmokeReportJsonContext.Default.SmokeReport));
        }
        catch (Exception ex) { SetStatus("Ошибка проверки", UserMessage(ex)); }
        finally { SetBusy(false); }
    }

    private async Task RunSmokeTestAsync(string runId)
    {
        if (isRunning) return;
        SetBusy(true);
        ClearSensitiveData();
        SetStatus("Проверка Debug", "Проверка на Android runtime…");
        try
        {
            var report = await Task.Run(() => CryptoSmokeTest.Run(runId));
            SetStatus("Проверка криптографии", $"{report.Checks.Count(check => check.Success)}/{report.Checks.Count}", report.Success);
            await File.WriteAllTextAsync(Path.Combine(FileSystem.AppDataDirectory, "crypto-smoke-result.json"),
                JsonSerializer.Serialize(report, SmokeReportJsonContext.Default.SmokeReport));
            global::Android.Util.Log.Info("PocketVaultCrypto", $"{runId}: {(report.Success ? "PASS" : "FAIL")}");
        }
        catch (Exception ex) { SetStatus("Ошибка проверки", UserMessage(ex)); }
        finally { SetBusy(false); }
    }
}
#endif
