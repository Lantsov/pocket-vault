using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Content;
using AndroidX.Activity.Result;
using AndroidX.Activity.Result.Contract;
using PocketVault.Android.Diagnostics;

namespace PocketVault.Android;

[Activity(Name = "com.lantsov.pocketvault.MainActivity", Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private ActivityResultLauncher? documentLauncher;
    private TaskCompletionSource<string?>? documentRequest;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
#if DEBUG
        if (Intent?.GetStringExtra("crypto_smoke_run_id") is { Length: > 0 and <= 64 } runId)
            SmokeTestRequest.RunId = runId;
        if (Intent?.GetStringExtra("document_smoke_run_id") is { Length: > 0 and <= 64 } documentRunId)
            SmokeTestRequest.DocumentRunId = documentRunId;
#endif
        base.OnCreate(savedInstanceState);
        documentLauncher = RegisterForActivityResult(new ActivityResultContracts.StartActivityForResult(),
            new DocumentResultCallback(this));
    }

    internal Task<string?> SelectDocumentAsync(Intent intent)
    {
        if (documentRequest is not null) throw new InvalidOperationException("Диалог уже открыт.");
        var request = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        documentRequest = request;
        try { documentLauncher!.Launch(intent); }
        catch { documentRequest = null; throw; }
        return request.Task;
    }

    protected override void OnDestroy()
    {
        documentRequest?.TrySetCanceled();
        documentRequest = null;
        base.OnDestroy();
    }

    private sealed class DocumentResultCallback(MainActivity activity) : Java.Lang.Object, IActivityResultCallback
    {
        public void OnActivityResult(Java.Lang.Object? result)
        {
            var request = activity.documentRequest;
            activity.documentRequest = null;
            if (result is ActivityResult response && response.ResultCode == (int)Result.Ok)
            {
                string? id = response.Data?.Data?.ToString();
                if (id is null) request?.TrySetException(new IOException("Провайдер не вернул документ."));
                else request?.TrySetResult(id);
            }
            else request?.TrySetResult(null);
        }
    }
}
